using ClosedXML.Excel;
using PhotoMatcher.Models;
using System.IO;

namespace PhotoMatcher.Services
{
    public static class ExcelExportService
    {
        public static string Export(List<MatchResult> results, string outputFolder)
        {
            Directory.CreateDirectory(outputFolder);

            var fileName = $"PhotoMatch_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var path = Path.Combine(outputFolder, fileName);

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("התאמות");

            sheet.Cell(1, 1).Value = "תמונה ישנה";
            sheet.Cell(1, 2).Value = "תמונה חדשה";
            sheet.Cell(1, 3).Value = "אחוז דמיון";
            sheet.Cell(1, 4).Value = "מספר פנים";
            sheet.Cell(1, 5).Value = "קבוצת כפילויות";
            sheet.Cell(1, 6).Value = "דירוג איכות (1=הכי טובה)";
            sheet.Cell(1, 7).Value = "הערה";
            sheet.Row(1).Style.Font.Bold = true;

            var ordered = results
                .Where(r => !r.IsBelowThreshold)
                .OrderBy(r => r.DuplicateGroup ?? int.MaxValue)
                .ThenBy(r => r.QualityRank ?? int.MaxValue)
                .ToList();

            int row = 2;
            foreach (var result in ordered)
            {
                sheet.Cell(row, 1).Value = result.OldImage;
                sheet.Cell(row, 2).Value = result.NewImage;
                sheet.Cell(row, 3).Value = result.Similarity;
                sheet.Cell(row, 4).Value = result.FaceCount;
                if (result.DuplicateGroup.HasValue)
                    sheet.Cell(row, 5).Value = result.DuplicateGroup.Value;
                if (result.QualityRank.HasValue)
                    sheet.Cell(row, 6).Value = result.QualityRank.Value;
                sheet.Cell(row, 7).Value = result.Comment;
                row++;
            }

            if (row > 2)
                sheet.Range(2, 3, row - 1, 3).Style.NumberFormat.Format = "0.0%";

            sheet.Columns().AdjustToContents();

            var belowThreshold = results.Where(r => r.IsBelowThreshold).ToList();
            if (belowThreshold.Count > 0)
            {
                var lowSheet = workbook.Worksheets.Add("מתחת לסף (לא מזוהה)");

                lowSheet.Cell(1, 1).Value = "תמונה ישנה";
                lowSheet.Cell(1, 2).Value = "תמונה קרובה ביותר במאגר הישיבה";
                lowSheet.Cell(1, 3).Value = "אחוז דמיון (הכי גבוה שנמצא)";
                lowSheet.Cell(1, 4).Value = "מספר פנים";
                lowSheet.Cell(1, 5).Value = "הערה";
                lowSheet.Row(1).Style.Font.Bold = true;

                int lowRow = 2;
                foreach (var result in belowThreshold.OrderByDescending(r => r.Similarity))
                {
                    lowSheet.Cell(lowRow, 1).Value = result.OldImage;
                    lowSheet.Cell(lowRow, 2).Value = result.ClosestCandidateImage;
                    lowSheet.Cell(lowRow, 3).Value = result.Similarity;
                    lowSheet.Cell(lowRow, 4).Value = result.FaceCount;
                    lowSheet.Cell(lowRow, 5).Value = result.Comment;
                    lowRow++;
                }

                if (lowRow > 2)
                    lowSheet.Range(2, 3, lowRow - 1, 3).Style.NumberFormat.Format = "0.0%";

                lowSheet.Columns().AdjustToContents();
            }

            workbook.SaveAs(path);
            return path;
        }
    }
}
