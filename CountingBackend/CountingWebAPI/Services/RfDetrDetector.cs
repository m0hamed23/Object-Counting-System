using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Extensions.Logging;
using CountingWebAPI.Models.Processing;
using System.Threading.Tasks;
using SkiaSharp;
using SessionOptions = Microsoft.ML.OnnxRuntime.SessionOptions;

namespace CountingWebAPI.Services
{
    public class RfDetrDetector : IDetector
    {
        private readonly InferenceSession _session;
        private readonly string[] _classNames;
        private readonly ILogger<RfDetrDetector> _logger;
        
        private readonly int _inputWidth;
        private readonly int _inputHeight;
        private readonly string _boxesOutputName;
        private readonly string _logitsOutputName;

        private readonly object _inferLock = new object();

        public RfDetrDetector(string modelPath, string[] classNames, ILogger<RfDetrDetector> logger)
        {
            _classNames = classNames ?? throw new ArgumentNullException(nameof(classNames));
            _logger = logger;
            try
            {
                var sessionOptions = CreateSessionOptionsWithExecutionProviderPriority();
                _session = new InferenceSession(modelPath, sessionOptions);

                // Dynamically get input dimensions from the model
                var inputInfo = _session.InputMetadata.Values.First();
                _inputHeight = inputInfo.Dimensions[2];
                _inputWidth = inputInfo.Dimensions[3];

                // Dynamically identify output tensor names based on their shape
                _boxesOutputName = _session.OutputMetadata.Keys.First(k => _session.OutputMetadata[k].Dimensions.Last() == 4);
                _logitsOutputName = _session.OutputMetadata.Keys.First(k => _session.OutputMetadata[k].Dimensions.Last() == 91);

                _logger.LogInformation("RF-DETR model loaded: {ModelPath}. Input: {Width}x{Height}. Boxes output: '{BoxesName}', Logits output: '{LogitsName}'", 
                    modelPath, _inputWidth, _inputHeight, _boxesOutputName, _logitsOutputName);
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
                _logger.LogInformation("ONNX Runtime: CUDA execution provider successfully enabled for RF-DETR.");
                return sessionOptions; // Success, we're done.
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ONNX Runtime: CUDA provider not available for RF-DETR. Trying next provider.");
            }

            // 2. Try for CoreML (Apple Silicon / macOS)
            try
            {
                sessionOptions.AppendExecutionProvider_CoreML(CoreMLFlags.COREML_FLAG_ONLY_ENABLE_DEVICE_WITH_ANE);
                _logger.LogInformation("ONNX Runtime: CoreML execution provider successfully enabled for RF-DETR (leveraging GPU and/or Neural Engine on macOS).");
                return sessionOptions; // Success, we're done.
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ONNX Runtime: CoreML provider not available for RF-DETR. This is expected on non-Apple platforms. Trying next provider.");
            }

            // 3. Try for ROCm (AMD)
            try
            {
                sessionOptions.AppendExecutionProvider_ROCm(0);
                _logger.LogInformation("ONNX Runtime: ROCm execution provider successfully enabled for RF-DETR.");
                return sessionOptions; // Success
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ONNX Runtime: ROCm provider not available for RF-DETR. Trying next provider.");
            }
            
            // 4. Try for OpenVINO (Intel)
            try
            {
                sessionOptions.AppendExecutionProvider_OpenVINO();
                _logger.LogInformation("ONNX Runtime: OpenVINO execution provider successfully enabled for RF-DETR.");
                return sessionOptions; // Success
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ONNX Runtime: OpenVINO provider not available for RF-DETR. Trying next provider.");
            }

            // 5. Fallback to CPU
            _logger.LogInformation("ONNX Runtime: No specialized hardware execution provider available for RF-DETR. Falling back to CPU.");
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
                    var inputTensor = PreprocessImage(image);
                    var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input", inputTensor) };

                    using (var results = _session.Run(inputs))
                    {
                        var logitsTensor = results.First(r => r.Name == _logitsOutputName).AsTensor<float>();
                        var boxesTensor = results.First(r => r.Name == _boxesOutputName).AsTensor<float>();

                        var detections = ParseOutput(logitsTensor, boxesTensor, confidenceThreshold, targetClassesStr, image.Width, image.Height);
                        return ApplyNMS(detections, nmsThreshold);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during RF-DETR detection pipeline");
                    return new List<DetectionResult>();
                }
            }
        }

        private unsafe DenseTensor<float> PreprocessImage(SKBitmap originalImage)
        {
            var tensor = new DenseTensor<float>(new[] { 1, 3, _inputHeight, _inputWidth });
            var resizeInfo = new SKImageInfo(_inputWidth, _inputHeight, SKColorType.Rgb888x, SKAlphaType.Opaque);

            using (var resized = originalImage.Resize(resizeInfo, SKFilterQuality.Medium))
            {
                float[] mean = { 0.485f, 0.456f, 0.406f };
                float[] std = { 0.229f, 0.224f, 0.225f };

                byte* pixels = (byte*)resized.GetPixels().ToPointer();
                int rowBytes = resized.RowBytes;

                Parallel.For(0, _inputHeight, y =>
                {
                    for (int x = 0; x < _inputWidth; x++)
                    {
                        int pixelIndex = y * rowBytes + x * 4;
                        
                        float r_norm = pixels[pixelIndex + 2] / 255.0f; // Red
                        float g_norm = pixels[pixelIndex + 1] / 255.0f; // Green
                        float b_norm = pixels[pixelIndex + 0] / 255.0f; // Blue

                        tensor[0, 0, y, x] = (r_norm - mean[0]) / std[0]; // R
                        tensor[0, 1, y, x] = (g_norm - mean[1]) / std[1]; // G
                        tensor[0, 2, y, x] = (b_norm - mean[2]) / std[2]; // B
                    }
                });
            }
            return tensor;
        }

        private static float Sigmoid(float value)
        {
            return 1.0f / (1.0f + (float)Math.Exp(-value));
        }

        private List<DetectionResult> ParseOutput(Tensor<float> logitsTensor, Tensor<float> boxesTensor, float confidenceThreshold, string? targetClassesStr, int originalImageWidth, int originalImageHeight)
        {
            var detections = new List<DetectionResult>();
            int numQueries = logitsTensor.Dimensions[1];
            int numClasses = logitsTensor.Dimensions[2];

            var targetClasses = string.IsNullOrWhiteSpace(targetClassesStr)
                ? new HashSet<string>()
                : new HashSet<string>(targetClassesStr.Split(',').Select(c => c.Trim().ToLowerInvariant()));

            for (int query = 0; query < numQueries; query++)
            {
                float bestScore = 0;
                int bestClassIndex = -1;

                for (int cls = 0; cls < numClasses; cls++)
                {
                    float score = Sigmoid(logitsTensor[0, query, cls]);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestClassIndex = cls;
                    }
                }
                
                if (bestScore > confidenceThreshold)
                {
                    int correctedClassIndex = bestClassIndex -1;
                    if (correctedClassIndex < 0 || correctedClassIndex >= _classNames.Length) continue;

                    string className = _classNames[correctedClassIndex];
                    if (targetClasses.Any() && !targetClasses.Contains(className.ToLowerInvariant())) continue;

                    float cx = boxesTensor[0, query, 0];
                    float cy = boxesTensor[0, query, 1];
                    float w = boxesTensor[0, query, 2];
                    float h = boxesTensor[0, query, 3];

                    float boxX = (cx - 0.5f * w) * originalImageWidth;
                    float boxY = (cy - 0.5f * h) * originalImageHeight;
                    float boxWidth = w * originalImageWidth;
                    float boxHeight = h * originalImageHeight;
                    
                    detections.Add(new DetectionResult
                    {
                        Box = new RectangleF(boxX, boxY, boxWidth, boxHeight),
                        Confidence = bestScore,
                        ClassName = className,
                        ClassIndex = correctedClassIndex
                    });
                }
            }

            return detections;
        }

        private List<DetectionResult> ApplyNMS(List<DetectionResult> detections, float nmsThreshold)
        {
            var selectedDetections = new List<DetectionResult>();
            foreach (var grp in detections.GroupBy(d => d.ClassIndex))
            {
                var sortedDetections = grp.OrderByDescending(d => d.Confidence).ToList();
                while (sortedDetections.Any())
                {
                    var best = sortedDetections.First();
                    selectedDetections.Add(best);
                    sortedDetections.Remove(best);
                    sortedDetections = sortedDetections
                        .Where(d => CalculateIoU(best.Box, d.Box) < nmsThreshold)
                        .ToList();
                }
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

        public void Dispose()
        {
            _session?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}