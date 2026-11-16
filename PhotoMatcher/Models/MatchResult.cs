namespace PhotoMatcher.Models
{
    public class MatchResult
    {
        public string NewImage { get; set; }

        public string NewImagePath { get; set; }

        public string OldImage { get; set; }

        public string OldImagePath { get; set; }

        public double Similarity { get; set; }

        public int FaceCount { get; set; }

        public bool HasFace { get; set; }

        public int? DuplicateGroup { get; set; }

        public int? QualityRank { get; set; }

        public string Comment { get; set; }

        public bool IsBelowThreshold { get; set; }

        public string ClosestCandidateImage { get; set; }
    }
}