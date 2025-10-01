using System.Drawing;

namespace CountingWebAPI.Models.Processing // Adjust namespace
{
    public class YoloDetection
    {
        public RectangleF Box { get; set; } // The bounding box of the detection.
        public string ClassName { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public int ClassIndex { get; set; }
    }
}