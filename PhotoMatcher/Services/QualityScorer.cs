using OpenCvSharp;

namespace PhotoMatcher.Services
{
    public static class QualityScorer
    {
        public static double ComputeSharpness(Mat image, Rect faceBox)
        {
            using var region = CropToImage(image, faceBox);
            using var gray = new Mat();
            Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);

            using var laplacian = new Mat();
            Cv2.Laplacian(gray, laplacian, MatType.CV_64F);

            Cv2.MeanStdDev(laplacian, out _, out var stdDev);
            return stdDev.Val0 * stdDev.Val0;
        }

        public static double ComputeExposureScore(Mat image)
        {
            using var gray = new Mat();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

            Cv2.MeanStdDev(gray, out var mean, out _);
            double totalPixels = gray.Rows * gray.Cols;

            using var darkMask = new Mat();
            Cv2.Threshold(gray, darkMask, 5, 255, ThresholdTypes.BinaryInv);
            double darkRatio = Cv2.CountNonZero(darkMask) / totalPixels;

            using var brightMask = new Mat();
            Cv2.Threshold(gray, brightMask, 250, 255, ThresholdTypes.Binary);
            double brightRatio = Cv2.CountNonZero(brightMask) / totalPixels;

            double idealDistance = Math.Abs(mean.Val0 - 128) / 128.0;
            double score = 1.0 - (darkRatio + brightRatio) - idealDistance * 0.5;

            return Math.Max(0, Math.Min(1, score));
        }

        private static Mat CropToImage(Mat image, Rect box)
        {
            int x = Math.Max(0, box.X);
            int y = Math.Max(0, box.Y);
            int width = Math.Min(box.Width, image.Width - x);
            int height = Math.Min(box.Height, image.Height - y);

            if (width <= 0 || height <= 0)
                return image.Clone();

            return new Mat(image, new Rect(x, y, width, height));
        }
    }
}
