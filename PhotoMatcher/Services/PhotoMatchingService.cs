using PhotoMatcher.Helpers;
using PhotoMatcher.Models;
using System.IO;

namespace PhotoMatcher.Services
{
    public class PhotoMatchingService
    {
        public const double DefaultThreshold = 0.4;

        private readonly FaceRecognitionService _recognition;

        public PhotoMatchingService(FaceRecognitionService recognition)
        {
            _recognition = recognition;
        }

        public List<MatchResult> Match(
            List<string> oldImages,
            List<string> newImages,
            double threshold,
            IProgress<(int Done, int Total, string Message)>? progress = null)
        {
            int total = oldImages.Count + newImages.Count;
            int done = 0;

            var newFaces = new List<FaceInfo>();

            foreach (var path in newImages)
            {
                var face = TryGetMainFace(path, out var error);
                if (face != null)
                    newFaces.Add(face);

                done++;
                progress?.Report((done, total, $"מעבד תמונות חדשות: {Path.GetFileName(path)}"));
            }

            var oldFacesByPath = new Dictionary<string, FaceInfo?>();
            var oldErrorsByPath = new Dictionary<string, string?>();

            foreach (var oldPath in oldImages)
            {
                oldFacesByPath[oldPath] = TryGetMainFace(oldPath, out var error);
                oldErrorsByPath[oldPath] = error;

                done++;
                progress?.Report((done, total, $"משווה: {Path.GetFileName(oldPath)}"));
            }

            var duplicateGroups = DuplicateGroupingService.Group(
                oldFacesByPath.Values.Where(f => f != null).Select(f => f!).ToList());

            var results = new List<MatchResult>();

            foreach (var oldPath in oldImages)
            {
                var oldFileName = Path.GetFileName(oldPath);
                var oldFace = oldFacesByPath[oldPath];
                var error = oldErrorsByPath[oldPath];
                duplicateGroups.TryGetValue(oldPath, out var duplicateInfo);

                if (error != null)
                {
                    results.Add(new MatchResult
                    {
                        OldImage = oldFileName,
                        OldImagePath = oldPath,
                        NewImage = "",
                        Similarity = 0,
                        FaceCount = 0,
                        Comment = $"שגיאה בעיבוד התמונה: {error}"
                    });
                    continue;
                }

                if (oldFace == null)
                {
                    results.Add(new MatchResult
                    {
                        OldImage = oldFileName,
                        OldImagePath = oldPath,
                        NewImage = "",
                        Similarity = 0,
                        FaceCount = 0,
                        Comment = "לא זוהו פנים בתמונה"
                    });
                    continue;
                }

                var allSimilarities = newFaces
                    .Select(nf => new { Face = nf, Similarity = SimilarityHelper.CosineSimilarity(oldFace.Embedding, nf.Embedding) })
                    .OrderByDescending(m => m.Similarity)
                    .ToList();

                var matches = allSimilarities.Where(m => m.Similarity >= threshold).ToList();

                if (matches.Count == 0)
                {
                    var bestBelowThreshold = allSimilarities.FirstOrDefault();
                    results.Add(new MatchResult
                    {
                        OldImage = oldFileName,
                        OldImagePath = oldPath,
                        NewImage = "",
                        Similarity = bestBelowThreshold?.Similarity ?? 0,
                        FaceCount = oldFace.FaceCount,
                        HasFace = true,
                        DuplicateGroup = duplicateInfo?.GroupId,
                        QualityRank = duplicateInfo?.QualityRank,
                        Comment = "לא נמצאה התאמה מעל הסף",
                        IsBelowThreshold = true,
                        ClosestCandidateImage = bestBelowThreshold?.Face.FileName ?? ""
                    });
                    continue;
                }

                foreach (var match in matches)
                {
                    results.Add(new MatchResult
                    {
                        OldImage = oldFileName,
                        OldImagePath = oldPath,
                        NewImage = match.Face.FileName,
                        NewImagePath = match.Face.FullPath,
                        Similarity = match.Similarity,
                        FaceCount = oldFace.FaceCount,
                        HasFace = true,
                        DuplicateGroup = duplicateInfo?.GroupId,
                        QualityRank = duplicateInfo?.QualityRank,
                        Comment = ""
                    });
                }
            }

            return results;
        }

        private FaceInfo? TryGetMainFace(string path, out string? error)
        {
            try
            {
                error = null;
                return _recognition.GetMainFace(path);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }
    }
}
