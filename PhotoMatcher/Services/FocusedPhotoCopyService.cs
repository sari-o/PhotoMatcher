using OpenCvSharp;
using PhotoMatcher.Models;
using System.IO;

namespace PhotoMatcher.Services
{
    public class FocusedPhotoCopySummary
    {
        public int Copied { get; set; }

        public List<string> Skipped { get; set; } = new();
    }

    /// <summary>
    /// Copies the best photo of each group, cropped around the main person (including the
    /// area below the face where hands/study material typically are) and with any other
    /// people in frame blurred out.
    /// </summary>
    public static class FocusedPhotoCopyService
    {
        // Crop margins, relative to the main face's bounding box.
        private const double CropSideMargin = 0.9;
        private const double CropTopMargin = 0.7;
        private const double CropBottomMargin = 1.6;

        // How far around each other detected face to expand the blur region.
        // Kept modest so it covers hair/ears without spilling into a neighboring face.
        private const double OtherFaceBlurMargin = 0.35;

        // Extra safety padding (relative to the main face's own box) that is always
        // restored to the original, sharp pixels after blurring, in case a blur region
        // from another face happens to overlap the main subject.
        private const double MainFaceProtectMargin = 0.5;

        // Blur kernel size as a fraction of the blurred region's width.
        private const double BlurKernelRatio = 0.18;
        private const int MinBlurKernel = 21;
        private const int MaxBlurKernel = 199;

        public static FocusedPhotoCopySummary CopyAndFocusBestOfEachGroup(List<MatchResult> results, string destinationFolder)
        {
            Directory.CreateDirectory(destinationFolder);

            var summary = new FocusedPhotoCopySummary();
            var bestRows = BestPhotoCopyService.SelectBestRows(results);

            var detectorPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AI", "Models", "det_10g.onnx");
            using var detector = new FaceDetector(detectorPath);

            foreach (var row in bestRows)
            {
                if (!File.Exists(row.OldImagePath))
                {
                    summary.Skipped.Add($"{row.OldImage} (הקובץ המקורי לא נמצא)");
                    continue;
                }

                var extension = Path.GetExtension(row.OldImagePath);
                var baseName = !string.IsNullOrEmpty(row.NewImage)
                    ? Path.GetFileNameWithoutExtension(row.NewImage)
                    : Path.GetFileNameWithoutExtension(row.OldImagePath);

                var destPath = BestPhotoCopyService.GetUniqueDestinationPath(destinationFolder, baseName, extension);

                try
                {
                    ProcessImage(row.OldImagePath, destPath, detector);
                    summary.Copied++;
                }
                catch (Exception ex)
                {
                    summary.Skipped.Add($"{row.OldImage} (שגיאה בעיבוד: {ex.Message})");
                }
            }

            return summary;
        }

        private static void ProcessImage(string sourcePath, string destPath, FaceDetector detector)
        {
            using var image = Cv2.ImRead(sourcePath);
            if (image.Empty())
                throw new InvalidOperationException("לא ניתן לפתוח את התמונה");

            var faces = detector.Detect(image);
            if (faces.Count == 0)
            {
                File.Copy(sourcePath, destPath, overwrite: true);
                return;
            }

            var (mainFace, _) = FaceDetector.SelectMainFace(faces, image.Width, image.Height);

            // Keep a sharp copy of the main subject's own face so it can be restored
            // afterwards, in case another face's blur region happens to overlap it.
            var protectedBox = ExpandBox(
                mainFace.BBox, MainFaceProtectMargin, MainFaceProtectMargin, MainFaceProtectMargin, MainFaceProtectMargin,
                image.Width, image.Height);
            using var protectedOriginal = new Mat(image, protectedBox).Clone();

            foreach (var face in faces)
            {
                if (face == mainFace)
                    continue;

                BlurRegion(image, ExpandBox(face.BBox, OtherFaceBlurMargin, OtherFaceBlurMargin, OtherFaceBlurMargin, OtherFaceBlurMargin, image.Width, image.Height));
            }

            protectedOriginal.CopyTo(new Mat(image, protectedBox));

            var cropBox = ExpandBox(
                mainFace.BBox,
                CropSideMargin,
                CropSideMargin,
                CropTopMargin,
                CropBottomMargin,
                image.Width,
                image.Height);

            using var cropped = new Mat(image, cropBox);
            Cv2.ImWrite(destPath, cropped);
        }

        private static Rect ExpandBox(Rect box, double leftFactor, double rightFactor, double topFactor, double bottomFactor, int imageWidth, int imageHeight)
        {
            int left = (int)(box.Width * leftFactor);
            int right = (int)(box.Width * rightFactor);
            int top = (int)(box.Height * topFactor);
            int bottom = (int)(box.Height * bottomFactor);

            int x1 = Math.Max(0, box.X - left);
            int y1 = Math.Max(0, box.Y - top);
            int x2 = Math.Min(imageWidth, box.X + box.Width + right);
            int y2 = Math.Min(imageHeight, box.Y + box.Height + bottom);

            return new Rect(x1, y1, x2 - x1, y2 - y1);
        }

        private static void BlurRegion(Mat image, Rect region)
        {
            if (region.Width <= 0 || region.Height <= 0)
                return;

            int kernel = (int)(region.Width * BlurKernelRatio);
            kernel = Math.Clamp(kernel, MinBlurKernel, MaxBlurKernel);
            if (kernel % 2 == 0)
                kernel++;

            using var roi = new Mat(image, region);
            using var blurred = new Mat();
            Cv2.GaussianBlur(roi, blurred, new Size(kernel, kernel), 0);
            blurred.CopyTo(roi);
        }
    }
}
