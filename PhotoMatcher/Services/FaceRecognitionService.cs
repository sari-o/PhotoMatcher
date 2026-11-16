using OpenCvSharp;
using PhotoMatcher.Models;
using System.IO;

namespace PhotoMatcher.Services
{
    public class FaceRecognitionService : IDisposable
    {
        private readonly FaceDetector _detector;
        private readonly FaceEmbedder _embedder;

        public FaceRecognitionService()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var detectorPath = Path.Combine(baseDir, "AI", "Models", "det_10g.onnx");
            var embedderPath = Path.Combine(baseDir, "AI", "Models", "w600k_r50.onnx");

            _detector = new FaceDetector(detectorPath);
            _embedder = new FaceEmbedder(embedderPath);
        }

        public FaceInfo? GetMainFace(string imagePath)
        {
            using var image = Cv2.ImRead(imagePath);
            if (image.Empty())
                throw new InvalidOperationException($"Cannot open image: {imagePath}");

            var faces = _detector.Detect(image);
            if (faces.Count == 0)
                return null;

            var (mainFace, mainFaceScore) = FaceDetector.SelectMainFace(faces, image.Width, image.Height);

            using var aligned = _embedder.AlignFace(image, mainFace.Landmarks);
            var embedding = _embedder.GetEmbedding(aligned);

            return new FaceInfo
            {
                FileName = Path.GetFileName(imagePath),
                FullPath = imagePath,
                BoundingBox = mainFace.BBox,
                Embedding = embedding,
                FaceCount = faces.Count,
                MainFaceScore = mainFaceScore,
                DetectionConfidence = mainFace.Score,
                Sharpness = QualityScorer.ComputeSharpness(image, mainFace.BBox),
                ExposureScore = QualityScorer.ComputeExposureScore(image)
            };
        }

        public void Dispose()
        {
            _detector.Dispose();
            _embedder.Dispose();
        }
    }
}
