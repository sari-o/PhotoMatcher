using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace PhotoMatcher.Services
{
    public class DetectedFace
    {
        public Rect BBox { get; set; }

        public Point2f[] Landmarks { get; set; } = Array.Empty<Point2f>();

        public float Score { get; set; }
    }

    public class FaceDetector : IDisposable
    {
        private const int InputSize = 640;
        private const float ScoreThreshold = 0.5f;
        private const float NmsThreshold = 0.4f;
        private const int NumAnchors = 2;
        private static readonly int[] Strides = { 8, 16, 32 };

        private readonly InferenceSession _session;

        public FaceDetector(string modelPath)
        {
            _session = new InferenceSession(modelPath);
        }

        public List<DetectedFace> Detect(Mat image)
        {
            using var letterboxed = Letterbox(image, InputSize, out float scale);
            var blob = BuildBlob(letterboxed);

            var inputName = _session.InputMetadata.Keys.First();
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, blob) };

            using var results = _session.Run(inputs);

            var scoresByCount = new Dictionary<int, float[]>();
            var bboxesByCount = new Dictionary<int, float[]>();
            var kpsByCount = new Dictionary<int, float[]>();

            foreach (var result in results)
            {
                var tensor = result.AsTensor<float>();
                int count = tensor.Dimensions[0];
                int channels = tensor.Dimensions[1];
                var data = tensor.ToArray();

                if (channels == 1) scoresByCount[count] = data;
                else if (channels == 4) bboxesByCount[count] = data;
                else if (channels == 10) kpsByCount[count] = data;
            }

            var candidates = new List<DetectedFace>();

            foreach (var stride in Strides)
            {
                int gridSize = InputSize / stride;
                int count = gridSize * gridSize * NumAnchors;

                if (!scoresByCount.TryGetValue(count, out var scores) ||
                    !bboxesByCount.TryGetValue(count, out var bboxes) ||
                    !kpsByCount.TryGetValue(count, out var kps))
                {
                    continue;
                }

                for (int i = 0; i < count; i++)
                {
                    float score = scores[i];
                    if (score < ScoreThreshold)
                        continue;

                    int locationIndex = i / NumAnchors;
                    float centerX = (locationIndex % gridSize) * stride;
                    float centerY = (locationIndex / gridSize) * stride;

                    float x1 = centerX - bboxes[i * 4 + 0] * stride;
                    float y1 = centerY - bboxes[i * 4 + 1] * stride;
                    float x2 = centerX + bboxes[i * 4 + 2] * stride;
                    float y2 = centerY + bboxes[i * 4 + 3] * stride;

                    var landmarks = new Point2f[5];
                    for (int k = 0; k < 5; k++)
                    {
                        float lx = centerX + kps[i * 10 + k * 2 + 0] * stride;
                        float ly = centerY + kps[i * 10 + k * 2 + 1] * stride;
                        landmarks[k] = new Point2f(lx, ly);
                    }

                    candidates.Add(new DetectedFace
                    {
                        BBox = new Rect((int)x1, (int)y1, (int)(x2 - x1), (int)(y2 - y1)),
                        Landmarks = landmarks,
                        Score = score
                    });
                }
            }

            var kept = Nms(candidates, NmsThreshold);

            foreach (var face in kept)
            {
                face.BBox = new Rect(
                    (int)(face.BBox.X / scale),
                    (int)(face.BBox.Y / scale),
                    (int)(face.BBox.Width / scale),
                    (int)(face.BBox.Height / scale));

                for (int k = 0; k < face.Landmarks.Length; k++)
                {
                    face.Landmarks[k] = new Point2f(face.Landmarks[k].X / scale, face.Landmarks[k].Y / scale);
                }
            }

            return kept;
        }

        private static Mat Letterbox(Mat src, int targetSize, out float scale)
        {
            float imRatio = (float)src.Height / src.Width;
            int newWidth, newHeight;

            if (imRatio > 1f)
            {
                newHeight = targetSize;
                newWidth = (int)(newHeight / imRatio);
            }
            else
            {
                newWidth = targetSize;
                newHeight = (int)(newWidth * imRatio);
            }

            scale = (float)newHeight / src.Height;

            using var resized = new Mat();
            Cv2.Resize(src, resized, new Size(newWidth, newHeight));

            var canvas = new Mat(targetSize, targetSize, MatType.CV_8UC3, Scalar.Black);
            resized.CopyTo(canvas[new Rect(0, 0, newWidth, newHeight)]);
            return canvas;
        }

        private static DenseTensor<float> BuildBlob(Mat img)
        {
            int size = img.Rows;
            var tensor = new DenseTensor<float>(new[] { 1, 3, size, size });

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var pixel = img.At<Vec3b>(y, x);
                    tensor[0, 0, y, x] = (pixel.Item2 - 127.5f) / 128f;
                    tensor[0, 1, y, x] = (pixel.Item1 - 127.5f) / 128f;
                    tensor[0, 2, y, x] = (pixel.Item0 - 127.5f) / 128f;
                }
            }

            return tensor;
        }

        private static List<DetectedFace> Nms(List<DetectedFace> boxes, float iouThreshold)
        {
            var sorted = boxes.OrderByDescending(b => b.Score).ToList();
            var suppressed = new bool[sorted.Count];
            var result = new List<DetectedFace>();

            for (int i = 0; i < sorted.Count; i++)
            {
                if (suppressed[i])
                    continue;

                result.Add(sorted[i]);

                for (int j = i + 1; j < sorted.Count; j++)
                {
                    if (suppressed[j])
                        continue;

                    if (IoU(sorted[i].BBox, sorted[j].BBox) > iouThreshold)
                        suppressed[j] = true;
                }
            }

            return result;
        }

        private static float IoU(Rect a, Rect b)
        {
            int x1 = Math.Max(a.X, b.X);
            int y1 = Math.Max(a.Y, b.Y);
            int x2 = Math.Min(a.X + a.Width, b.X + b.Width);
            int y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

            int interWidth = Math.Max(0, x2 - x1);
            int interHeight = Math.Max(0, y2 - y1);
            float interArea = interWidth * interHeight;

            float unionArea = a.Width * a.Height + b.Width * b.Height - interArea;
            return unionArea <= 0 ? 0 : interArea / unionArea;
        }

        public void Dispose()
        {
            _session.Dispose();
        }

        public static (DetectedFace Face, double Score) SelectMainFace(List<DetectedFace> faces, int imageWidth, int imageHeight)
        {
            float centerX = imageWidth / 2f;
            float centerY = imageHeight / 2f;

            DetectedFace? best = null;
            double bestScore = double.NegativeInfinity;

            foreach (var face in faces)
            {
                double area = (double)face.BBox.Width * face.BBox.Height;
                double faceCenterX = face.BBox.X + face.BBox.Width / 2.0;
                double faceCenterY = face.BBox.Y + face.BBox.Height / 2.0;
                double distance = Math.Sqrt(Math.Pow(faceCenterX - centerX, 2) + Math.Pow(faceCenterY - centerY, 2));
                double score = area - distance * 100;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = face;
                }
            }

            return (best!, bestScore);
        }
    }
}
