using PhotoMatcher.Helpers;
using PhotoMatcher.Models;

namespace PhotoMatcher.Services
{
    public class DuplicateQualityInfo
    {
        public int GroupId { get; set; }

        public int QualityRank { get; set; }
    }

    public static class DuplicateGroupingService
    {
        public const double DefaultThreshold = 0.6;

        private const double SharpnessWeight = 0.5;
        private const double ExposureWeight = 0.3;
        private const double ConfidenceWeight = 0.2;

        public static Dictionary<string, DuplicateQualityInfo> Group(List<FaceInfo> faces, double threshold = DefaultThreshold)
        {
            var result = new Dictionary<string, DuplicateQualityInfo>();
            int n = faces.Count;
            if (n == 0)
                return result;

            var parent = new int[n];
            for (int i = 0; i < n; i++)
                parent[i] = i;

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }
                return x;
            }

            void Union(int a, int b)
            {
                int ra = Find(a), rb = Find(b);
                if (ra != rb)
                    parent[ra] = rb;
            }

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    var similarity = SimilarityHelper.CosineSimilarity(faces[i].Embedding, faces[j].Embedding);
                    if (similarity >= threshold)
                        Union(i, j);
                }
            }

            var clusters = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                int root = Find(i);
                if (!clusters.TryGetValue(root, out var list))
                    clusters[root] = list = new List<int>();
                list.Add(i);
            }

            int groupId = 1;
            foreach (var cluster in clusters.Values)
            {
                if (cluster.Count < 2)
                    continue;

                var members = cluster.Select(idx => faces[idx]).ToList();

                double minSharp = members.Min(f => f.Sharpness);
                double maxSharp = members.Max(f => f.Sharpness);
                double minExp = members.Min(f => f.ExposureScore);
                double maxExp = members.Max(f => f.ExposureScore);
                double minConf = members.Min(f => f.DetectionConfidence);
                double maxConf = members.Max(f => f.DetectionConfidence);

                double Normalize(double value, double min, double max) => max > min ? (value - min) / (max - min) : 1.0;

                var ranked = members
                    .Select(f => new
                    {
                        Face = f,
                        Composite =
                            SharpnessWeight * Normalize(f.Sharpness, minSharp, maxSharp) +
                            ExposureWeight * Normalize(f.ExposureScore, minExp, maxExp) +
                            ConfidenceWeight * Normalize(f.DetectionConfidence, minConf, maxConf)
                    })
                    .OrderByDescending(s => s.Composite)
                    .ToList();

                for (int rank = 0; rank < ranked.Count; rank++)
                {
                    result[ranked[rank].Face.FullPath] = new DuplicateQualityInfo
                    {
                        GroupId = groupId,
                        QualityRank = rank + 1
                    };
                }

                groupId++;
            }

            return result;
        }
    }
}
