using CountingWebAPI.Hubs;
using CountingWebAPI.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Drawing; // For RectangleF
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CountingWebAPI.Models.Processing;
using Microsoft.Extensions.Configuration;
using System.Runtime.InteropServices;
using CountingWebAPI.Models.Database;
using Microsoft.Extensions.Options;
using System.Globalization;
using SkiaSharp;

namespace CountingWebAPI.Services
{
    public class CameraProcessor : IDisposable
    {
        public event Action<int, int>? StateUpdated;
        public int Id => _cameraConfig.Id;
        public string Name => _cameraConfig.Name;
        private readonly BlockingCollection<SKBitmap> _frameQueue = new(1);
        private readonly Camera _cameraConfig;
        private readonly IHubContext<CrowdMonitorHub> _hub;
        private readonly ILogger<CameraProcessor> _log;
        private readonly SettingsService _sets;
        private readonly AppSettings _appSets;
        private readonly ILoggerFactory _loggerFactory;
        private readonly RoiService _roiService;
        private CancellationTokenSource? _cts;
        private TaskCompletionSource<bool>? _streamEndTcs;
        private RTSPSource? _rtspSource;
        private IDetector? _detector;
        
        private List<DetectionResult> _lastKnownDetections = new();
        public int TotalTrackedCount => _lastKnownDetections.Count;
        
        private string _currentStatus = "Inactive";
        private string? _lastFrameDataUrl = null;
        private readonly object _statusLock = new();
        private readonly object _frameLock = new();
        private List<List<double>> _roiNormalized = new();
        private readonly object _roiLock = new();
        private int _frameCountForInterval = 0;
        private float _yoloConfidenceThreshold;
        private float _nmsThreshold;
        private string? _targetClasses;
        
        private enum ProcessingTier { Active, IdleScan }
        private ProcessingTier _currentTier = ProcessingTier.IdleScan;
        private DateTime _lastMotionTime = DateTime.MinValue;
        private DateTime _lastYoloRunTime = DateTime.MinValue;
        private byte[]? _previousFrameGrayscaleBytes;
        private readonly object _motionLock = new object();

        private double _activeStateTimeoutSeconds;
        private double _idleScanModeIntervalSeconds;
        private bool _idleScanModeEnabled;
        private double _motionDetectionThreshold;
        private int _activeModeProcessNthFrame;
        private int _motionPixelDifferenceThreshold;

        public CameraProcessor(Camera cameraConfig, IHubContext<CrowdMonitorHub> hubCtx, IOptions<AppSettings> appOpt, SettingsService setSvc, IServiceScopeFactory sFact, ILoggerFactory loggerFactory, IConfiguration config, RoiService roiService, List<List<double>>? initialRoi)
        {
            _cameraConfig = cameraConfig;
            _hub = hubCtx;
            _log = loggerFactory.CreateLogger<CameraProcessor>();
            _appSets = appOpt.Value;
            _sets = setSvc;
            _loggerFactory = loggerFactory;
            _roiService = roiService;
            _roiNormalized = initialRoi ?? new List<List<double>>();
            _sets.OnSettingsChangedAsync += LoadDetectionParametersAsync;
            _lastMotionTime = DateTime.UtcNow;
        }

        private async Task InitializeDetectorAsync()
        {
            var settings = await _sets.GetAllSettingsAsDictionaryAsync(true);
            var modelType = settings.GetValueOrDefault("model_type", "YOLO");
            
            _log.LogInformation("Cam {id}: Initializing detector of type '{type}'.", _cameraConfig.Id, modelType);
            
            try
            {
                if (modelType.Equals("RF-DETR", StringComparison.OrdinalIgnoreCase))
                {
                    var modelPath = settings.GetValueOrDefault("rfdetr_model_path");
                    if (string.IsNullOrEmpty(modelPath))
                    {
                        _log.LogError("Cam {id}: RF-DETR model path is not configured in settings. Detection is disabled.", _cameraConfig.Id);
                        SetCurrentStatus("Error");
                        return;
                    }
                    
                    var fullPath = Path.Combine(AppContext.BaseDirectory, modelPath);
                    if (!File.Exists(fullPath))
                    {
                         _log.LogError("Cam {id}: RF-DETR model not found at {path}. Detection is disabled.", _cameraConfig.Id, fullPath);
                         SetCurrentStatus("Error");
                         return;
                    }
                    _detector = new RfDetrDetector(fullPath, YoloDetector.CocoClassNames, _loggerFactory.CreateLogger<RfDetrDetector>());
                }
                else // Default to YOLO
                {
                    var modelPath = settings.GetValueOrDefault("model_path");
                    if (string.IsNullOrEmpty(modelPath))
                    {
                        _log.LogError("Cam {id}: YOLO model path is not configured in settings. Detection is disabled.", _cameraConfig.Id);
                        SetCurrentStatus("Error");
                        return;
                    }

                    var fullPath = Path.Combine(AppContext.BaseDirectory, modelPath);
                     if (!File.Exists(fullPath))
                    {
                        _log.LogError("Cam {id}: YOLO model not found at {path}. Detection is disabled.", _cameraConfig.Id, fullPath);
                        SetCurrentStatus("Error");
                        return;
                    }
                    _detector = new YoloDetector(fullPath, YoloDetector.CocoClassNames, _loggerFactory.CreateLogger<YoloDetector>());
                }
            }
             catch (Exception ex)
            {
                _log.LogError(ex, "Cam {id}: Failed to initialize detector.", _cameraConfig.Id);
                SetCurrentStatus("Error");
            }
        }
        
        private void ProcessSingleFrame(SKBitmap frame, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;
            SetCurrentStatus("Normal");
            if (HasSignificantMotion(frame)) _lastMotionTime = DateTime.UtcNow;

            bool shouldRunYolo = false;
            
            bool isActivePhase = !_idleScanModeEnabled || (DateTime.UtcNow - _lastMotionTime).TotalSeconds < _activeStateTimeoutSeconds;

            if (isActivePhase)
            {
                if (_currentTier != ProcessingTier.Active) 
                {
                    _log.LogDebug("Cam {Id}: Entering ACTIVE mode.", Id);
                    _currentTier = ProcessingTier.Active;
                    _frameCountForInterval = 0;
                }
                
                if (++_frameCountForInterval >= _activeModeProcessNthFrame) 
                {
                    shouldRunYolo = true;
                    _frameCountForInterval = 0;
                }
            }
            else
            {
                if (_currentTier != ProcessingTier.IdleScan)
                {
                     _log.LogDebug("Cam {Id}: Entering IDLE SCAN mode.", Id);
                    _currentTier = ProcessingTier.IdleScan;
                }

                if ((DateTime.UtcNow - _lastYoloRunTime).TotalSeconds >= _idleScanModeIntervalSeconds)
                {
                    shouldRunYolo = true;
                }
            }
            
            List<DetectionResult> detectionsToDraw = new List<DetectionResult>();

            if (shouldRunYolo && _detector != null)
            {
                _log.LogTrace("Cam {Id}: Running AI detection in {Tier} mode.", Id, _currentTier);
                _lastYoloRunTime = DateTime.UtcNow;
                
                List<DetectionResult> newDetections;
                using (var maskedFrame = ApplyRoiMask(frame)) 
                {
                    newDetections = _detector.Detect(maskedFrame, _yoloConfidenceThreshold, _nmsThreshold, _targetClasses); 
                }
                
                _lastKnownDetections = newDetections;
                detectionsToDraw = newDetections;
            }
            
            StateUpdated?.Invoke(_cameraConfig.Id, TotalTrackedCount);
            
            DrawOverlays(frame, detectionsToDraw);
            
            EncodeAndEmitFrame(frame);
        }

        private void DrawOverlays(SKBitmap frame, List<DetectionResult> detections)
        {
            using (var canvas = new SKCanvas(frame))
            {
                lock (_roiLock)
                {
                    if (_roiNormalized.Any() && _roiNormalized.Count >= 3)
                    {
                        var points = _roiNormalized.Select(p => new SKPoint((float)(p[0] * frame.Width), (float)(p[1] * frame.Height))).ToArray();
                        using var paint = new SKPaint { Color = SKColors.Lime, StrokeWidth = 2, IsStroke = true, Style = SKPaintStyle.Stroke };
                        canvas.DrawPoints(SKPointMode.Polygon, points, paint);
                    }
                }
                
                using (var boxPaint = new SKPaint { Color = SKColors.Cyan, StrokeWidth = 2, IsStroke = true, Style = SKPaintStyle.Stroke })
                using (var textPaint = new SKPaint { Color = SKColors.Cyan, TextSize = 12, IsAntialias = true })
                {
                    foreach (var detection in detections)
                    {
                        var rect = SKRect.Create(detection.Box.X, detection.Box.Y, detection.Box.Width, detection.Box.Height);
                        canvas.DrawRect(rect, boxPaint);
                        canvas.DrawText($"{detection.ClassName}", rect.Left, rect.Top - 5, textPaint);
                    }
                }

                var modeText = "MODE: " + (_currentTier == ProcessingTier.Active ? "ACTIVE" : (_idleScanModeEnabled ? "SCANNING" : "STANDBY"));
                var modeColor = _currentTier == ProcessingTier.Active ? SKColors.LimeGreen : (_idleScanModeEnabled ? SKColors.Orange : SKColors.Gray);
                using (var modePaint = new SKPaint { Color = modeColor, TextSize = 14, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) })
                {
                    canvas.DrawText(modeText, 10, 20, modePaint);
                }
            }
        }

        private void ResetStateAndNotify() 
        { 
            _log.LogDebug("Cam {id}: Resetting state.", _cameraConfig.Id); 
            _lastKnownDetections.Clear();
            StateUpdated?.Invoke(_cameraConfig.Id, TotalTrackedCount); 
        }

        private async Task LoadDetectionParametersAsync()
        {
            _log.LogInformation("Cam {id}: Reloading settings.", _cameraConfig.Id);
            await InitializeDetectorAsync();
            var s = await _sets.GetAllSettingsAsDictionaryAsync(true);
            _yoloConfidenceThreshold = float.TryParse(s.GetValueOrDefault("confidence_threshold"), NumberStyles.Any, CultureInfo.InvariantCulture, out var conf) ? conf : 0.3f;
            _nmsThreshold = float.TryParse(s.GetValueOrDefault("nms_threshold"), NumberStyles.Any, CultureInfo.InvariantCulture, out var nms) ? nms : 0.45f;
            _targetClasses = s.GetValueOrDefault("target_classes", "person");
            _activeModeProcessNthFrame = int.TryParse(s.GetValueOrDefault("active_mode_process_nth_frame"), out var pfi) ? pfi : 5;
            if (_activeModeProcessNthFrame <= 0) _activeModeProcessNthFrame = 1;
            _idleScanModeEnabled = bool.TryParse(s.GetValueOrDefault("idle_scan_mode_enabled"), out var ism) ? ism : true;
            _idleScanModeIntervalSeconds = double.TryParse(s.GetValueOrDefault("idle_scan_mode_interval_seconds"), NumberStyles.Any, CultureInfo.InvariantCulture, out var hi) ? hi : 10.0;
            _activeStateTimeoutSeconds = double.TryParse(s.GetValueOrDefault("active_state_timeout_seconds"), NumberStyles.Any, CultureInfo.InvariantCulture, out var ast) ? ast : 15.0;
            _motionDetectionThreshold = double.TryParse(s.GetValueOrDefault("motion_detection_threshold"), NumberStyles.Any, CultureInfo.InvariantCulture, out var mdt) ? mdt : 0.005;
            _motionPixelDifferenceThreshold = int.TryParse(s.GetValueOrDefault("motion_pixel_difference_threshold"), out var mpdt) ? mpdt : 25;
        }

        private SKBitmap ApplyRoiMask(SKBitmap originalFrame)
        {
            SKPoint[] roiPoints;
            lock (_roiLock)
            {
                if (_roiNormalized == null || _roiNormalized.Count < 3)
                {
                    return originalFrame.Copy();
                }
                roiPoints = _roiNormalized.Select(p => new SKPoint((float)(p[0] * originalFrame.Width), (float)(p[1] * originalFrame.Height))).ToArray();
            }

            var masked = new SKBitmap(originalFrame.Info);
            using (var canvas = new SKCanvas(masked))
            {
                canvas.Clear(SKColors.Black);
                using (var path = new SKPath())
                {
                    path.AddPoly(roiPoints, true);
                    canvas.ClipPath(path, SKClipOperation.Intersect, true);
                }
                canvas.DrawBitmap(originalFrame, 0, 0);
            }
            return masked;
        }

        #region Boilerplate Code
        public Task StartAsync()
        {
            _cts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                await LoadDetectionParametersAsync();
                if (_detector == null)
                {
                    SetCurrentStatus("Error");
                    _log.LogError("Cam {id}: Cannot start because detector is not initialized.", _cameraConfig.Id);
                    EmitStatusUpdate("Detector failed to initialize.");
                    return;
                }
                _ = Task.Run(() => FrameProcessingWorker(_cts.Token), _cts.Token);
                await StartProcessingLoopAsync(_cts.Token);
            }, _cts.Token);
            return Task.CompletedTask;
        }
        private void FrameProcessingWorker(CancellationToken token)
        {
            _log.LogInformation("Cam {Id}: Frame processing worker started.", Id);
            try
            {
                foreach (var frame in _frameQueue.GetConsumingEnumerable(token))
                {
                    using (frame)
                    {
                        ProcessSingleFrame(frame, token);
                    }
                }
            }
            catch (OperationCanceledException) { _log.LogInformation("Cam {Id}: Frame processing worker shutting down.", Id); }
            catch (Exception ex) { _log.LogError(ex, "Cam {Id}: Unhandled exception in FrameProcessingWorker.", Id); }
        }
        private async Task StartProcessingLoopAsync(CancellationToken token)
        {
            bool hasStartedSuccessfully = false;
            while (!token.IsCancellationRequested)
            {
                _streamEndTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                try
                {
                    StartSource(); hasStartedSuccessfully = true; 
                    await _streamEndTcs.Task.WaitAsync(token);
                }
                catch (OperationCanceledException) { _log.LogInformation("Cam {id}: Processing loop's wait was cancelled.", _cameraConfig.Id); }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Cam {id}: A critical error occurred.", _cameraConfig.Id);
                    if (!hasStartedSuccessfully) { SetCurrentStatus("Error"); EmitStatusUpdate($"Failed to start: {ex.Message}"); break; }
                }
                finally { CleanupSource(); }
                if (token.IsCancellationRequested) break;
                if (hasStartedSuccessfully)
                {
                    SetCurrentStatus("Retrying"); ResetStateAndNotify(); EmitStatusUpdate("Stream disconnected. Retrying...");
                    try { await Task.Delay(5000, token); } catch (OperationCanceledException) { break; }
                }
            }
            if(GetCurrentStatus() != "Error") { SetCurrentStatus("Inactive"); }
            ResetStateAndNotify(); EmitStatusUpdate("Processing stopped.");
        }
        private void StartSource()
        {
            if (GetCurrentStatus() != "Inactive" && GetCurrentStatus() != "Retrying") { return; }
            SetCurrentStatus("Connecting"); EmitStatusUpdate();
            
            if (string.IsNullOrWhiteSpace(_cameraConfig.RtspUrl))
            {
                throw new InvalidOperationException($"Camera {_cameraConfig.Id} has no RTSP URL configured.");
            }
            
            _rtspSource = new RTSPSource(_cameraConfig.RtspUrl, _appSets, _log);
            _rtspSource.NewFrame += Rtsp_NewFrame; 
            _rtspSource.StreamEnded += StreamEnded_Handler; 
            _rtspSource.Start();
        }
        public void Stop() { _cts?.Cancel(); _frameQueue.CompleteAdding(); }
        private void Rtsp_NewFrame(object? sender, RawFrameData e) => AddFrameToQueue(() => CreateDeepCopyFromRawFrame(e));
        private void AddFrameToQueue(Func<SKBitmap?> createDeepCopy)
        {
            if (_cts == null || _cts.IsCancellationRequested || _frameQueue.IsAddingCompleted || _frameQueue.Count > 0) return;
            SKBitmap? deepCopy = null;
            try { deepCopy = createDeepCopy(); if (deepCopy != null) _frameQueue.Add(deepCopy); }
            catch (Exception ex) { _log.LogError(ex, "Cam {id}: Failed to copy and add frame.", Id); deepCopy?.Dispose(); }
        }
        private SKBitmap? CreateDeepCopyFromRawFrame(RawFrameData? e)
        {
            if (e == null || e.PixelData == null || e.Width <= 0 || e.Height <= 0) return null;
            var info = new SKImageInfo(e.Width, e.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var bmpWrapper = new SKBitmap();
            var pixelsHandle = GCHandle.Alloc(e.PixelData, GCHandleType.Pinned);
            try
            {
                var ptr = pixelsHandle.AddrOfPinnedObject();
                // Wrap the existing, pinned memory without copying.
                bmpWrapper.InstallPixels(info, ptr, info.RowBytes, (address, context) => { /* no-op for release */ }, null);
                // Create a new bitmap and copy the pixels from the wrapper. The new bitmap owns its memory.
                var deepCopy = new SKBitmap(info);
                bmpWrapper.CopyTo(deepCopy);
                return deepCopy;
            }
            finally
            {
                if (pixelsHandle.IsAllocated)
                {
                    pixelsHandle.Free();
                }
            }
        }
        private bool HasSignificantMotion(SKBitmap currentFrame)
        {
            const int W = 64, H = 48;
            byte[] currentBytes = new byte[W * H];
            
            var resizeInfo = new SKImageInfo(W, H, currentFrame.ColorType, currentFrame.AlphaType);
            using (var resized = currentFrame.Resize(resizeInfo, SKFilterQuality.Medium))
            {
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    SKColor color = resized.GetPixel(x,y);
                    currentBytes[y * W + x] = (byte)(color.Red*0.299 + color.Green*0.587 + color.Blue*0.114);
                }
            }
            lock (_motionLock)
            {
                if (_previousFrameGrayscaleBytes == null) { _previousFrameGrayscaleBytes = currentBytes; return false; }
                long changed = 0;
                for (int i = 0; i < currentBytes.Length; i++) if (Math.Abs(currentBytes[i] - _previousFrameGrayscaleBytes[i]) > _motionPixelDifferenceThreshold) changed++;
                _previousFrameGrayscaleBytes = currentBytes;
                return (double)changed / currentBytes.Length > _motionDetectionThreshold;
            }
        }
        private void CleanupSource()
        {
            if (_rtspSource != null) { _rtspSource.NewFrame -= Rtsp_NewFrame; _rtspSource.StreamEnded -= StreamEnded_Handler; _rtspSource.Stop(); _rtspSource.Dispose(); _rtspSource = null; }
        }
        private void StreamEnded_Handler(object? sender, StreamEndedEventArgs e) { _log.LogWarning("Stream for Cam {id} ended.", _cameraConfig.Id); _streamEndTcs?.TrySetResult(true); }
        private void EncodeAndEmitFrame(SKBitmap frame)
        {
            try
            {
                using var ms = new MemoryStream();
                using var skData = frame.Encode(SKEncodedImageFormat.Jpeg, 75);
                skData.SaveTo(ms);
                SetLastFrameDataUrl("data:image/jpeg;base64," + Convert.ToBase64String(ms.ToArray()));
            }
            catch (Exception ex) { _log.LogError(ex, "Cam {id}: Error encoding frame.", _cameraConfig.Id); SetLastFrameDataUrl(null); }
            finally { EmitStatusUpdate(); }
        }
        private void EmitStatusUpdate(string? msg = null)
        {
            var payload = new { cameraIndex = _cameraConfig.Id, name = _cameraConfig.Name, status = GetCurrentStatus(), frameUrl = GetLastFrameDataUrl() ?? "", totalTrackedCount = TotalTrackedCount, roi = _roiNormalized, message = msg ?? "" };
            _hub.Clients.All.SendAsync("camera_status", payload);
        }
        public async Task SetRoiAsync(List<List<double>> roi) { lock (_roiLock) { _roiNormalized = roi ?? new List<List<double>>(); } await _roiService.SaveRoiAsync(_cameraConfig.Id, roi); }
        public void Dispose() { _cts?.Dispose(); _detector?.Dispose(); }
        public string GetCurrentStatus() { lock (_statusLock) return _currentStatus; }
        private void SetCurrentStatus(string s) { lock (_statusLock) if (_currentStatus != s) { _currentStatus = s; _log.LogInformation($"Cam {_cameraConfig.Id} status: {s}"); } }
        public string? GetLastFrameDataUrl() { lock (_frameLock) return _lastFrameDataUrl; }
        private void SetLastFrameDataUrl(string? u) { lock (_frameLock) _lastFrameDataUrl = u; }
        public List<List<double>> GetRoi() { lock (_roiLock) return new List<List<double>>(_roiNormalized.Select(p => new List<double>(p))); }
        #endregion
    }
}