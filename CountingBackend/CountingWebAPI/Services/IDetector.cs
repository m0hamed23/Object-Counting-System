
using System;
using System.Collections.Generic;
using CountingWebAPI.Models.Processing;
using SkiaSharp;

namespace CountingWebAPI.Services
{
    public interface IDetector : IDisposable
    {
        List<DetectionResult> Detect(SKBitmap image, float confidenceThreshold, float nmsThreshold, string? targetClassesStr);
    }
}