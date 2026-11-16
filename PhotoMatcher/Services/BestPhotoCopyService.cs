using PhotoMatcher.Models;
using System.IO;

namespace PhotoMatcher.Services
{
    public class BestPhotoCopySummary
    {
        public int Copied { get; set; }

        public List<string> Skipped { get; set; } = new();
    }

    public static class BestPhotoCopyService
    {
        public static IEnumerable<MatchResult> SelectBestRows(List<MatchResult> results)
        {
            return results
                .Where(r => r.HasFace && (r.QualityRank == null || r.QualityRank == 1) && !string.IsNullOrEmpty(r.OldImagePath))
                .GroupBy(r => r.OldImagePath)
                .Select(g => g.OrderByDescending(r => r.Similarity).First());
        }

        public static BestPhotoCopySummary CopyBestOfEachGroup(List<MatchResult> results, string destinationFolder)
        {
            var rows = SelectBestRows(results);

            return CopyRows(
                rows,
                destinationFolder,
                row => row.OldImagePath,
                row => !string.IsNullOrEmpty(row.NewImage) ? Path.GetFileNameWithoutExtension(row.NewImage) : Path.GetFileNameWithoutExtension(row.OldImagePath),
                row => row.OldImage);
        }

        public static IEnumerable<MatchResult> SelectAllMatchedRows(List<MatchResult> results)
        {
            return results
                .Where(r => r.HasFace && !r.IsBelowThreshold && !string.IsNullOrEmpty(r.NewImagePath))
                .GroupBy(r => r.NewImagePath)
                .Select(g => g.OrderByDescending(r => r.Similarity).First());
        }

        public static BestPhotoCopySummary CopyAllMatches(List<MatchResult> results, string destinationFolder)
        {
            var rows = SelectAllMatchedRows(results);

            return CopyRows(
                rows,
                destinationFolder,
                row => row.NewImagePath,
                row => Path.GetFileNameWithoutExtension(row.NewImage),
                row => row.NewImage);
        }

        private static BestPhotoCopySummary CopyRows(
            IEnumerable<MatchResult> rows,
            string destinationFolder,
            Func<MatchResult, string> sourcePathSelector,
            Func<MatchResult, string> baseNameSelector,
            Func<MatchResult, string> displayNameSelector)
        {
            Directory.CreateDirectory(destinationFolder);

            var summary = new BestPhotoCopySummary();

            foreach (var row in rows)
            {
                var sourcePath = sourcePathSelector(row);

                if (!File.Exists(sourcePath))
                {
                    summary.Skipped.Add($"{displayNameSelector(row)} (הקובץ המקורי לא נמצא)");
                    continue;
                }

                var extension = Path.GetExtension(sourcePath);
                var baseName = baseNameSelector(row);

                var destPath = GetUniqueDestinationPath(destinationFolder, baseName, extension);

                File.Copy(sourcePath, destPath);
                summary.Copied++;
            }

            return summary;
        }

        public static string GetUniqueDestinationPath(string folder, string baseName, string extension)
        {
            var candidate = Path.Combine(folder, baseName + extension);
            var counter = 1;

            while (File.Exists(candidate))
            {
                candidate = Path.Combine(folder, $"{baseName}_{counter}{extension}");
                counter++;
            }

            return candidate;
        }
    }
}
