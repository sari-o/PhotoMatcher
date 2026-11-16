using PhotoMatcher.Services;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PhotoMatcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<Models.MatchResult> _lastResults = new();
        private string? _lastExcelPath;

        public MainWindow()
        {
            InitializeComponent();

        }

        private void BrowseOldFolder_Click(object sender, RoutedEventArgs e)
        {
            BrowseFolder(txtOldFolder);
        }

        private void BrowseNewFolder_Click(object sender, RoutedEventArgs e)
        {
            BrowseFolder(txtNewFolder);
        }

        private static void BrowseFolder(TextBox target)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();

            if (Directory.Exists(target.Text))
                dialog.InitialDirectory = target.Text;

            if (dialog.ShowDialog() == true)
                target.Text = dialog.FolderName;
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            btnRun.IsEnabled = false;
            progressBar.Value = 0;
            txtStatus.Text = "";

            try
            {
                var loader = new ImageLoader();
                var oldImages = loader.Load(txtOldFolder.Text);
                var newImages = loader.Load(txtNewFolder.Text, recursive: true);

                if (!double.TryParse(txtThreshold.Text, out var threshold))
                    threshold = PhotoMatchingService.DefaultThreshold;

                var progress = new Progress<(int Done, int Total, string Message)>(p =>
                {
                    progressBar.Maximum = p.Total;
                    progressBar.Value = p.Done;
                    txtStatus.Text = p.Message;
                });

                var results = await Task.Run(() =>
                {
                    using var recognition = new FaceRecognitionService();
                    var matcher = new PhotoMatchingService(recognition);
                    return matcher.Match(oldImages, newImages, threshold, progress);
                });

                var outputFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output");
                var excelPath = ExcelExportService.Export(results, outputFolder);

                _lastResults = results;
                _lastExcelPath = excelPath;
                btnCopyBest.IsEnabled = true;
                btnCopyAllMatches.IsEnabled = true;
                btnCopyFocused.IsEnabled = true;
               // btnRetryUnmatched.IsEnabled = true;

                txtStatus.Text = "הושלם";

                MessageBox.Show(
                    $"נמצאו {results.Count} שורות התאמה.\nהקובץ נשמר ב:\n{excelPath}",
                    "סיום",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{excelPath}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                txtStatus.Text = "שגיאה";
                MessageBox.Show($"שגיאה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnRun.IsEnabled = true;
            }
        }

        private void CopyBest_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var summary = BestPhotoCopyService.CopyBestOfEachGroup(_lastResults, dialog.FolderName);

                if (!string.IsNullOrEmpty(_lastExcelPath) && File.Exists(_lastExcelPath))
                {
                    var excelDest = System.IO.Path.Combine(dialog.FolderName, System.IO.Path.GetFileName(_lastExcelPath));
                    File.Copy(_lastExcelPath, excelDest, overwrite: true);
                }

                var message = $"הועתקו {summary.Copied} תמונות לתיקייה:\n{dialog.FolderName}\n\nקובץ האקסל הועתק גם הוא לתיקייה, למעקב אחר תמונות לא מזוהות.";
                if (summary.Skipped.Count > 0)
                    message += $"\n\nדולגו {summary.Skipped.Count} תמונות:\n{string.Join("\n", summary.Skipped)}";

                MessageBox.Show(message, "העתקה הושלמה", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בהעתקה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyAllMatches_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var summary = BestPhotoCopyService.CopyAllMatches(_lastResults, dialog.FolderName);

                if (!string.IsNullOrEmpty(_lastExcelPath) && File.Exists(_lastExcelPath))
                {
                    var excelDest = System.IO.Path.Combine(dialog.FolderName, System.IO.Path.GetFileName(_lastExcelPath));
                    File.Copy(_lastExcelPath, excelDest, overwrite: true);
                }

                var message = $"הועתקו {summary.Copied} תמונות לתיקייה:\n{dialog.FolderName}\n\nקובץ האקסל הועתק גם הוא לתיקייה, למעקב אחר תמונות לא מזוהות.";
                if (summary.Skipped.Count > 0)
                    message += $"\n\nדולגו {summary.Skipped.Count} תמונות:\n{string.Join("\n", summary.Skipped)}";

                MessageBox.Show(message, "העתקה הושלמה", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בהעתקה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CopyFocused_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();

            if (dialog.ShowDialog() != true)
                return;

            btnCopyFocused.IsEnabled = false;

            try
            {
                var summary = await Task.Run(() =>
                    FocusedPhotoCopyService.CopyAndFocusBestOfEachGroup(_lastResults, dialog.FolderName));

                var message = $"הועתקו ותוקנו {summary.Copied} תמונות (חיתוך סביב האדם המרכזי + טשטוש אנשים נוספים) לתיקייה:\n{dialog.FolderName}";
                if (summary.Skipped.Count > 0)
                    message += $"\n\nדולגו {summary.Skipped.Count} תמונות:\n{string.Join("\n", summary.Skipped)}";

                MessageBox.Show(message, "העתקה ותיקון הושלמו", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בעיבוד: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnCopyFocused.IsEnabled = true;
            }
        }

        private List<string> GetUnmatchedOldImagePaths()
        {
            return _lastResults
                .Where(r => string.IsNullOrEmpty(r.NewImage) && r.HasFace)
                .Select(r => r.OldImagePath)
                .Distinct()
                .ToList();
        }

        private async void RetryUnmatched_Click(object sender, RoutedEventArgs e)
        {
            var unmatchedPaths = GetUnmatchedOldImagePaths();

            if (unmatchedPaths.Count == 0)
            {
                MessageBox.Show(
                    "לא נמצאו תמונות שלא זוהו להרצה מחדש.",
                    "אין תמונות שלא זוהו",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "בחר תיקייה נוספת להשוואה" };

            if (dialog.ShowDialog() != true)
                return;

            btnRun.IsEnabled = false;
            btnCopyBest.IsEnabled = false;
            btnCopyAllMatches.IsEnabled = false;
            btnCopyFocused.IsEnabled = false;
           // btnRetryUnmatched.IsEnabled = false;
            progressBar.Value = 0;
            txtStatus.Text = "";

            try
            {
                var loader = new ImageLoader();
                var newImages = loader.Load(dialog.FolderName, recursive: true);

                if (!double.TryParse(txtThreshold.Text, out var threshold))
                    threshold = PhotoMatchingService.DefaultThreshold;

                var progress = new Progress<(int Done, int Total, string Message)>(p =>
                {
                    progressBar.Maximum = p.Total;
                    progressBar.Value = p.Done;
                    txtStatus.Text = p.Message;
                });

                var newResults = await Task.Run(() =>
                {
                    using var recognition = new FaceRecognitionService();
                    var matcher = new PhotoMatchingService(recognition);
                    return matcher.Match(unmatchedPaths, newImages, threshold, progress);
                });

                var groupOffset = _lastResults
                    .Where(r => r.DuplicateGroup.HasValue)
                    .Select(r => r.DuplicateGroup!.Value)
                    .DefaultIfEmpty(0)
                    .Max();

                foreach (var result in newResults)
                {
                    if (result.DuplicateGroup.HasValue)
                        result.DuplicateGroup += groupOffset;
                }

                var unmatchedSet = unmatchedPaths.ToHashSet();
                _lastResults.RemoveAll(r =>
                    unmatchedSet.Contains(r.OldImagePath) && string.IsNullOrEmpty(r.NewImage) && r.HasFace);
                _lastResults.AddRange(newResults);

                var outputFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output");
                var excelPath = ExcelExportService.Export(_lastResults, outputFolder);
                _lastExcelPath = excelPath;

                var stillUnmatched = newResults.Count(r => string.IsNullOrEmpty(r.NewImage));
                var foundNow = unmatchedPaths.Count - stillUnmatched;
                txtStatus.Text = "הושלם";

                MessageBox.Show(
                    $"הורצו {unmatchedPaths.Count} תמונות שלא זוהו מול התיקייה הנוספת.\n" +
                    $"נמצאו התאמות ל-{foundNow} מתוכן.\n" +
                    $"הקובץ עודכן ב:\n{excelPath}",
                    "סיום הרצה נוספת",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{excelPath}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                txtStatus.Text = "שגיאה";
                MessageBox.Show($"שגיאה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnRun.IsEnabled = true;
                btnCopyBest.IsEnabled = true;
                btnCopyAllMatches.IsEnabled = true;
                btnCopyFocused.IsEnabled = true;
              //  btnRetryUnmatched.IsEnabled = true;
            }
        }
    }
}
