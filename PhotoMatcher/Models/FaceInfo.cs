
using System.Numerics;
using OpenCvSharp;

namespace PhotoMatcher.Models
{
    public class FaceInfo
    {
        public string FileName { get; set; }

        public string FullPath { get; set; }

        public Rect BoundingBox { get; set; }

        public float[] Embedding { get; set; }

        public int FaceCount { get; set; }

        public double MainFaceScore { get; set; }

        public double DetectionConfidence { get; set; }

        public double Sharpness { get; set; }

        public double ExposureScore { get; set; }
    }
}
