using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace PhotoMatcher.Services
{
    public class FaceEmbedder : IDisposable
    {
        private const int AlignedSize = 112;

        private static readonly Point2f[] ArcFaceTemplate =
        {
            new Point2f(38.2946f, 51.6963f),
            new Point2f(73.5318f, 51.5014f),
            new Point2f(56.0252f, 71.7366f),
            new Point2f(41.5493f, 92.3655f),
            new Point2f(70.7299f, 92.2041f),
        };

        private readonly InferenceSession _session;

        public FaceEmbedder(string modelPath)
        {
            _session = new InferenceSession(modelPath);
        }

        public Mat AlignFace(Mat image, Point2f[] landmarks)
        {
            if (landmarks.Length != 5)
                throw new ArgumentException("Expected 5 facial landmarks", nameof(landmarks));

            using var srcPoints = InputArray.Create(landmarks);
            using var dstPoints = InputArray.Create(ArcFaceTemplate);
            using var transform = Cv2.EstimateAffinePartial2D(srcPoints, dstPoints);
            if (transform.Empty())
                throw new InvalidOperationException("Failed to estimate face alignment transform");

            var aligned = new Mat();
            Cv2.WarpAffine(image, aligned, transform, new Size(AlignedSize, AlignedSize));
            return aligned;
        }

        public float[] GetEmbedding(Mat alignedFace)
        {
            var blob = BuildBlob(alignedFace);

            var inputName = _session.InputMetadata.Keys.First();
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, blob) };

            using var results = _session.Run(inputs);
            return results.First().AsTensor<float>().ToArray();
        }

        private static DenseTensor<float> BuildBlob(Mat img)
        {
            var tensor = new DenseTensor<float>(new[] { 1, 3, AlignedSize, AlignedSize });

            for (int y = 0; y < AlignedSize; y++)
            {
                for (int x = 0; x < AlignedSize; x++)
                {
                    var pixel = img.At<Vec3b>(y, x);
                    tensor[0, 0, y, x] = (pixel.Item2 - 127.5f) / 127.5f;
                    tensor[0, 1, y, x] = (pixel.Item1 - 127.5f) / 127.5f;
                    tensor[0, 2, y, x] = (pixel.Item0 - 127.5f) / 127.5f;
                }
            }

            return tensor;
        }

        public void Dispose()
        {
            _session.Dispose();
        }
    }
}
