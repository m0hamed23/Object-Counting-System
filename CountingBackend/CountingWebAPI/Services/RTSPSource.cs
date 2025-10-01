using FFmpeg.AutoGen;
using CountingWebAPI.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace CountingWebAPI.Services
{
    #region RTSP Helper Classes & Enums
    public class RawFrameData
    {
        public byte[] PixelData { get; }
        public int Width { get; }
        public int Height { get; }
        public int Stride { get; }
        public AVPixelFormat PixelFormat { get; }
        public RawFrameData(byte[] pixelData, int width, int height, int stride, AVPixelFormat pixelFormat)
        { PixelData = pixelData; Width = width; Height = height; Stride = stride; PixelFormat = pixelFormat; }
    }

    public enum StreamEndReason { Stopped, EndOfFile, Error }

    public class StreamEndedEventArgs : EventArgs
    {
        public StreamEndReason Reason { get; }
        public string? ErrorMessage { get; }
        public StreamEndedEventArgs(StreamEndReason reason, string? errorMessage = null)
        { Reason = reason; ErrorMessage = errorMessage; }
    }
    #endregion

    public unsafe class RTSPSource : IDisposable
    {
        #region Fields
        private AVFormatContext* _formatContext;
        private AVCodecContext* _codecContext;
        private AVFrame* _frame;
        private AVPacket* _packet;
        private int _videoStreamIndex = -1;
        private volatile bool _isPlaying;
        private volatile bool _isDisposed;
        private Thread? _decodingThread;

        private readonly string _rtspUrl;
        private readonly ILogger _logger;
        private readonly AppSettings _appSettings;
        private readonly int _ffmpegLogLevel = ffmpeg.AV_LOG_WARNING;
        private readonly int _swsConversionFlags = ffmpeg.SWS_FAST_BILINEAR;
        #endregion

        public event EventHandler<RawFrameData>? NewFrame;
        public event EventHandler<StreamEndedEventArgs>? StreamEnded;

        public RTSPSource(string rtspUrl, AppSettings appSettings, ILogger logger)
        {
            _rtspUrl = rtspUrl ?? throw new ArgumentNullException(nameof(rtspUrl));
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            _logger = logger;
            _logger.LogDebug("Initializing FFmpeg network for RTSP source...");
            ffmpeg.avformat_network_init();
            ffmpeg.av_log_set_level(_ffmpegLogLevel);
        }

        public void Start()
        {
            if (_isPlaying) { _logger.LogWarning("Start called on RTSP source, but it's already playing."); return; }
            if (_isDisposed) { _logger.LogWarning("Start called on disposed RTSP source."); return; }

            try
            {
                _logger.LogInformation("Opening RTSP stream from URL: {RtspUrl}", _rtspUrl);
                OpenStream();
                _isPlaying = true;
                _decodingThread = new Thread(DecodeFrames) { IsBackground = true, Name = $"RTSPDecode_{Path.GetFileNameWithoutExtension(_rtspUrl)}" };
                _decodingThread.Start();
                _logger.LogInformation("RTSP decoding thread started for {RtspUrl}", _rtspUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start RTSP source for URL: {RtspUrl}", _rtspUrl);
                CloseStream();
                _isPlaying = false;
                throw;
            }
        }

        private void OpenStream()
        {
            _formatContext = ffmpeg.avformat_alloc_context();
            if (_formatContext == null) throw new OutOfMemoryException("Failed to allocate AVFormatContext.");

            AVDictionary* options = null;
            ffmpeg.av_dict_set(&options, "timeout", "5000000", 0);
            ffmpeg.av_dict_set(&options, "rtsp_transport", "tcp", 0);

            int ret;
            fixed (AVFormatContext** formatContextPtr = &_formatContext)
            {
                ret = ffmpeg.avformat_open_input(formatContextPtr, _rtspUrl, null, &options);
            }

            if (options != null) ffmpeg.av_dict_free(&options);

            if (ret != 0)
            {
                CloseStream();
                throw new InvalidOperationException($"Failed to open RTSP stream '{_rtspUrl}'. Error {ret}: {GetFFmpegError(ret)}");
            }

            if (ffmpeg.avformat_find_stream_info(_formatContext, null) < 0)
            {
                CloseStream();
                throw new InvalidOperationException("Could not find stream info.");
            }

            _videoStreamIndex = ffmpeg.av_find_best_stream(_formatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
            if (_videoStreamIndex < 0)
            {
                CloseStream();
                throw new InvalidOperationException("Could not find a video stream.");
            }

            AVStream* videoStream = _formatContext->streams[_videoStreamIndex];
            AVCodec* codec = ffmpeg.avcodec_find_decoder(videoStream->codecpar->codec_id);
            if (codec == null) { CloseStream(); throw new InvalidOperationException("Codec not found."); }

            _codecContext = ffmpeg.avcodec_alloc_context3(codec);
            if (_codecContext == null) { CloseStream(); throw new OutOfMemoryException("Failed to allocate AVCodecContext."); }

            if (ffmpeg.avcodec_parameters_to_context(_codecContext, videoStream->codecpar) < 0)
            {
                CloseStream();
                throw new InvalidOperationException("Failed to copy codec parameters.");
            }

            if (ffmpeg.avcodec_open2(_codecContext, codec, null) < 0)
            {
                CloseStream();
                throw new InvalidOperationException("Failed to open codec.");
            }

            _frame = ffmpeg.av_frame_alloc();
            _packet = ffmpeg.av_packet_alloc();
        }

        private void DecodeFrames()
        {
            StreamEndReason endReason = StreamEndReason.Error;
            string? errorMessage = "Decoding loop exited unexpectedly.";
            try
            {
                while (_isPlaying)
                {
                    if (_isDisposed) break;

                    ffmpeg.av_packet_unref(_packet);
                    int readResult = ffmpeg.av_read_frame(_formatContext, _packet);

                    if (readResult >= 0)
                    {
                        if (_packet->stream_index == _videoStreamIndex)
                        {
                            SendAndReceiveFrames(_packet);
                        }
                    }
                    else
                    {
                        errorMessage = GetFFmpegError(readResult);
                        _logger.LogWarning("av_read_frame error {ErrorCode} ({ErrorMessage}).", readResult, errorMessage);
                        endReason = readResult == ffmpeg.AVERROR_EOF ? StreamEndReason.EndOfFile : StreamEndReason.Error;
                        _isPlaying = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception in decoding loop for {RtspUrl}", _rtspUrl);
                errorMessage = ex.Message;
                endReason = StreamEndReason.Error;
                _isPlaying = false;
            }
            finally
            {
                if (!_isDisposed)
                {
                    OnStreamEnded(endReason, errorMessage);
                }
            }
        }

        private void SendAndReceiveFrames(AVPacket* packet)
        {
            if (_isDisposed || _codecContext == null || _frame == null) return;
            int ret = ffmpeg.avcodec_send_packet(_codecContext, packet);
            if (ret < 0) return;

            while (ret >= 0)
            {
                if (_isDisposed) break;
                ret = ffmpeg.avcodec_receive_frame(_codecContext, _frame);
                if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN) || ret == ffmpeg.AVERROR_EOF) break;
                if (ret < 0) break;
                ProcessFrameAndRaiseEvent(_frame);
                ffmpeg.av_frame_unref(_frame);
            }
        }
        private void ProcessFrameAndRaiseEvent(AVFrame* frame)
        {
            if (_isDisposed) return;
            int width = frame->width;
            int height = frame->height;
            if (width <= 0 || height <= 0 || frame->data[0] == null) return;

            const AVPixelFormat TargetFormat = AVPixelFormat.AV_PIX_FMT_BGRA;

            // --- FIX for 'deprecated pixel format' warning ---
            // The warning is triggered when sws_getContext receives a format like YUVJ420P (J for JPEG).
            // To fix it, we replace the deprecated format with its modern equivalent (e.g., YUV420P)
            // and then explicitly tell the scaler that the source uses the full JPEG color range.
            var sourceFormat = (AVPixelFormat)frame->format;
            var correctedSourceFormat = sourceFormat;
            bool isJpegRange = false;

            switch (sourceFormat)
            {
                case AVPixelFormat.AV_PIX_FMT_YUVJ420P: correctedSourceFormat = AVPixelFormat.AV_PIX_FMT_YUV420P; isJpegRange = true; break;
                case AVPixelFormat.AV_PIX_FMT_YUVJ422P: correctedSourceFormat = AVPixelFormat.AV_PIX_FMT_YUV422P; isJpegRange = true; break;
                case AVPixelFormat.AV_PIX_FMT_YUVJ444P: correctedSourceFormat = AVPixelFormat.AV_PIX_FMT_YUV444P; isJpegRange = true; break;
            }

            SwsContext* swsContext = ffmpeg.sws_getContext(width, height, correctedSourceFormat, width, height, TargetFormat, _swsConversionFlags, null, null, null);
            if (swsContext == null) return;

            // If we corrected a JPEG format, we must explicitly tell swscaler that the source has a full color range.
            if (isJpegRange)
            {
                ffmpeg.av_opt_set_int(swsContext, "src_range", 1, 0); // 1 = Full range (JPEG)
            }

            int bufferStride = width * 4;
            byte[] pixelBuffer = new byte[bufferStride * height];

            try
            {
                fixed (byte* pPixelBuffer = pixelBuffer)
                {
                    byte_ptrArray4 dstData = new byte_ptrArray4 { [0] = pPixelBuffer };
                    int_array4 dstLinesize = new int_array4 { [0] = bufferStride };

                    ffmpeg.sws_scale(swsContext, frame->data, frame->linesize, 0, height, dstData, dstLinesize);
                }
                NewFrame?.Invoke(this, new RawFrameData(pixelBuffer, width, height, bufferStride, TargetFormat));
            }
            finally
            {
                ffmpeg.sws_freeContext(swsContext);
            }
        }
        public void Stop()
        {
            if (!_isPlaying) return;
            _isPlaying = false;
            _decodingThread?.Join(TimeSpan.FromSeconds(2));
            CloseStream();
            OnStreamEnded(StreamEndReason.Stopped);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Stop();
            StreamEnded = null;
            NewFrame = null;
            GC.SuppressFinalize(this);
        }

        private void CloseStream()
        {
            if (_packet != null) { AVPacket* p = _packet; ffmpeg.av_packet_free(&p); _packet = null; }
            if (_frame != null) { AVFrame* f = _frame; ffmpeg.av_frame_free(&f); _frame = null; }
            if (_codecContext != null) { AVCodecContext* c = _codecContext; ffmpeg.avcodec_free_context(&c); _codecContext = null; }
            if (_formatContext != null) { AVFormatContext* fc = _formatContext; ffmpeg.avformat_close_input(&fc); _formatContext = null; }
        }

        private static string GetFFmpegError(int error)
        {
            byte* buffer = stackalloc byte[256];
            ffmpeg.av_strerror(error, buffer, 256);
            return Marshal.PtrToStringAnsi((IntPtr)buffer) ?? $"Unknown FFmpeg Error {error}";
        }

        private void OnStreamEnded(StreamEndReason reason, string? errorMessage = null)
        {
            if (_isDisposed) return;
            StreamEnded?.Invoke(this, new StreamEndedEventArgs(reason, errorMessage));
        }
    }
}