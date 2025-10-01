using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Extensions.Logging;
using CountingWebAPI.Models.Processing;
using SkiaSharp;
using System.Threading.Tasks;
using SessionOptions = Microsoft.ML.OnnxRuntime.SessionOptions;

namespace CountingWebAPI.Services
{
    public class YoloDetector : IDetector
    {
        private readonly InferenceSession _session;
        private readonly string[] _classNames;
        private readonly ILogger<YoloDetector> _logger;

        public const int TargetWidth = 640;
        public const int TargetHeight = 640;
        public static readonly string[] CocoClassNames = new string[] {
            "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat", "traffic light",
            "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow",
            "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee",
            "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle",
            "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange",
            "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch", "potted plant", "bed",
            "dining table", "toilet", "tv", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven",
            "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush"
        };

        private float _currentScale = 1f;
        private float _currentPadX = 0f;
        private float _currentPadY = 0f;
        private readonly object _inferLock = new object();

        public YoloDetector(string modelPath, string[] classNames, ILogger<YoloDetector> logger)
        {
            _classNames = classNames ?? throw new ArgumentNullException(nameof(classNames));
            _logger = logger;
            try
            {
                var sessionOptions = CreateSessionOptionsWithExecutionProviderPriority();
                _session = new InferenceSession(modelPath, sessionOptions);
                _logger.LogInformation("YOLO model loaded successfully: {ModelPath}", modelPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error during ONNX model loading or InferenceSession creation for model: {ModelPath}", modelPath);
                throw;
            }
        }
        
        private SessionOptions CreateSessionOptionsWithExecutionProviderPriority()
        {
            var sessionOptions = new SessionOptions { LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING };

            // 1. Try for CUDA (NVIDIA)
            try
            {
                sessionOptions.AppendExecutionProvider_CUDA(0);
                _logger.LogInformation("ONNX Runtime: CUDA execution provider successfully enabled for YOLO.");
                return sessionOptions; // Success, so we're done.
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ONNX Runtime: CUDA provider not available for YOLO. Trying next provider.");
            }

            // 2. Try for CoreML (Apple Silicon / macOS)
            try
            {
                sessionOptions.AppendExecutionProvider_CoreML(CoreMLFlags.COREML_FLAG_ONLY_ENABLE_DEVICE_WITH_ANE);
                _logger.LogInformation("ONNX Runtime: CoreML execution provider successfully enabled for YOLO (leveraging GPU and/or Neural Engine on macOS).");
                return sessionOptions; // Success, we're done.
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ONNX Runtime: CoreML provider not available for YOLO. This is expected on non-Apple platforms. Trying next provider.");
            }

            // 3. Try for ROCm (AMD)
            try
            {
                sessionOptions.AppendExecutionProvider_ROCm(0);
                _logger.LogInformation("ONNX Runtime: ROCm execution provider successfully enabled for YOLO.");
                return sessionOptions; // Success
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ONNX Runtime: ROCm provider not available for YOLO. Trying next provider.");
            }
            
            // 4. Try for OpenVINO (Intel)
            try
            {
                sessionOptions.AppendExecutionProvider_OpenVINO();
                _logger.LogInformation("ONNX Runtime: OpenVINO execution provider successfully enabled for YOLO.");
                return sessionOptions; // Success
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ONNX Runtime: OpenVINO provider not available for YOLO. Trying next provider.");
            }

            // 5. Fallback to CPU
            _logger.LogInformation("ONNX Runtime: No specialized hardware execution provider available for YOLO. Falling back to CPU.");
            return sessionOptions; // Return the options object which will default to CPU.
        }

        public List<DetectionResult> Detect(SKBitmap image, float confidenceThreshold, float nmsThreshold, string? targetClassesStr)
        {
            if (image == null)
            {
                _logger.LogWarning("Detect called with null image.");
                return new List<DetectionResult>();
            }

            lock (_inferLock)
            {
                try
                {
                    var (inputTensor, scale, padX, padY) = PreprocessImage(image);
                    _currentScale = scale;
                    _currentPadX = padX;
                    _currentPadY = padY;

                    var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_session.InputMetadata.Keys.First(), inputTensor) };

                    using (var results = _session.Run(inputs))
                    {
                        var outputTensor = results.First().AsTensor<float>();

                        if (outputTensor.Dimensions.Length == 3 && outputTensor.Dimensions[1] == 84 && outputTensor.Dimensions[2] > 84)
                        {
                            outputTensor = TransposeOutput(outputTensor);
                        }

                        var detections = ParseOutput(outputTensor, confidenceThreshold, targetClassesStr);
                        var nmsResults = ApplyNMS(detections, nmsThreshold);

                        foreach (var result in nmsResults)
                        {
                            result.Box = DenormalizeBox(result.Box, image.Width, image.Height);
                        }
                        return nmsResults;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during YOLO detection pipeline");
                    return new List<DetectionResult>();
                }
            }
        }
        
        private Tensor<float> TransposeOutput(Tensor<float> source)
        {
            var dimensions = source.Dimensions;
            int D0 = dimensions[0];
            int D1 = dimensions[1];
            int D2 = dimensions[2];

            var transposedTensor = new DenseTensor<float>(new[] { D0, D2, D1 });

            for (int j = 0; j < D1; j++)
            {
                for (int k = 0; k < D2; k++)
                {
                    transposedTensor[0, k, j] = source[0, j, k];
                }
            }
            return transposedTensor;
        }

        private unsafe (DenseTensor<float> tensor, float scale, float padX, float padY) PreprocessImage(SKBitmap originalImage)
        {
            int originalWidth = originalImage.Width;
            int originalHeight = originalImage.Height;

            float rW = (float)TargetWidth / originalWidth;
            float rH = (float)TargetHeight / originalHeight;
            float scale = Math.Min(rW, rH);

            int newUnpadWidth = (int)Math.Round(originalWidth * scale);
            int newUnpadHeight = (int)Math.Round(originalHeight * scale);

            float padX = (TargetWidth - newUnpadWidth) / 2f;
            float padY = (TargetHeight - newUnpadHeight) / 2f;

            var tensor = new DenseTensor<float>(new[] { 1, 3, TargetHeight, TargetWidth });

            var letterboxInfo = new SKImageInfo(TargetWidth, TargetHeight, SKColorType.Rgb888x, SKAlphaType.Opaque);
            using(var letterboxImage = new SKBitmap(letterboxInfo))
            using(var canvas = new SKCanvas(letterboxImage))
            {
                canvas.Clear(new SKColor(114, 114, 114)); // Letterbox color

                var scaledInfo = new SKImageInfo(newUnpadWidth, newUnpadHeight);
                using (var scaledBitmap = originalImage.Resize(scaledInfo, SKFilterQuality.High))
                {
                    canvas.DrawBitmap(scaledBitmap, padX, padY);
                }

                // Get a pointer to the raw pixel data for fast access.
                byte* pixels = (byte*)letterboxImage.GetPixels().ToPointer();
                int rowBytes = letterboxImage.RowBytes;

                Parallel.For(0, TargetHeight, y =>
                {
                    for (int x = 0; x < TargetWidth; x++)
                    {
                        int pixelIndex = y * rowBytes + x * 4; // 4 bytes per pixel (BGRA or RGBA)
                        tensor[0, 0, y, x] = pixels[pixelIndex + 2] / 255.0f;   // R
                        tensor[0, 1, y, x] = pixels[pixelIndex + 1] / 255.0f; // G
                        tensor[0, 2, y, x] = pixels[pixelIndex + 0] / 255.0f;  // B
                    }
                });
            }
            return (tensor, scale, padX, padY);
        }

        private List<DetectionResult> ParseOutput(Tensor<float> output, float confidenceThreshold, string? targetClassesStr)
        {
            var detections = new List<DetectionResult>();
            int numBoxes = output.Dimensions[1];
            int numClasses = output.Dimensions[2] - 4;

            var targetClasses = string.IsNullOrWhiteSpace(targetClassesStr)
                ? new HashSet<string>()
                : new HashSet<string>(targetClassesStr.Split(',').Select(c => c.Trim().ToLowerInvariant()));

            for (int i = 0; i < numBoxes; i++)
            {
                float maxClassConf = 0;
                int maxClassIdx = 0;
                for (int j = 0; j < numClasses; j++)
                {
                    float conf = output[0, i, j + 4];
                    if (conf > maxClassConf)
                    {
                        maxClassConf = conf;
                        maxClassIdx = j;
                    }
                }

                if (maxClassConf >= confidenceThreshold)
                {
                    string className = maxClassIdx < _classNames.Length ? _classNames[maxClassIdx] : "unknown";
                    if (targetClasses.Any() && !targetClasses.Contains(className.ToLowerInvariant())) continue;

                    float cx = output[0, i, 0]; float cy = output[0, i, 1];
                    float w = output[0, i, 2]; float h = output[0, i, 3];
                    detections.Add(new DetectionResult
                    {
                        // Box values are in pixels relative to the 640x640 input space
                        Box = new RectangleF(cx - w / 2, cy - h / 2, w, h),
                        Confidence = maxClassConf,
                        ClassName = className,
                        ClassIndex = maxClassIdx
                    });
                }
            }
            return detections;
        }

        private List<DetectionResult> ApplyNMS(List<DetectionResult> detections, float nmsThreshold)
        {
            var selectedDetections = new List<DetectionResult>();
            var sortedDetections = detections.OrderByDescending(d => d.Confidence).ToList();

            while (sortedDetections.Any())
            {
                var best = sortedDetections.First();
                selectedDetections.Add(best);
                sortedDetections.Remove(best);

                sortedDetections = sortedDetections
                    .Where(d => CalculateIoU(best.Box, d.Box) < nmsThreshold)
                    .ToList();
            }
            return selectedDetections;
        }

        private float CalculateIoU(RectangleF boxA, RectangleF boxB)
        {
            float xA = Math.Max(boxA.Left, boxB.Left);
            float yA = Math.Max(boxA.Top, boxB.Top);
            float xB = Math.Min(boxA.Right, boxB.Right);
            float yB = Math.Min(boxA.Bottom, boxB.Bottom);
            float intersectionArea = Math.Max(0, xB - xA) * Math.Max(0, yB - yA);
            float boxAArea = boxA.Width * boxA.Height;
            float boxBArea = boxB.Width * boxB.Height;
            float unionArea = boxAArea + boxBArea - intersectionArea;
            return unionArea > 0 ? intersectionArea / unionArea : 0f;
        }

        public RectangleF DenormalizeBox(RectangleF boxIn640Space, int originalImageWidth, int originalImageHeight)
        {
            // The box is in the coordinate system of the 640x640 letter-boxed image.
            // We need to convert it back to the original image's coordinate system.
            
            // 1. Remove the padding from the coordinates.
            float unpaddedX = boxIn640Space.X - _currentPadX;
            float unpaddedY = boxIn640Space.Y - _currentPadY;

            // 2. Rescale the coordinates and dimensions back to the original image size.
            float originalX = unpaddedX / _currentScale;
            float originalY = unpaddedY / _currentScale;
            float originalWidth = boxIn640Space.Width / _currentScale;
            float originalHeight = boxIn640Space.Height / _currentScale;

            return new RectangleF(originalX, originalY, originalWidth, originalHeight);
        }

        public void Dispose()
        {
            _session?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}