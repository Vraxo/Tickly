using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tickly.Models;
using Tickly.Services;
using Tickly.Utils;
using Tickly.Views.Plotting;

namespace Tickly.ViewModels;

public sealed partial class StatsViewModel : ObservableObject
{
    private readonly ProgressStorageService _progressStorageService; // Changed
    private const string ProgressExportFilePrefix = "Tickly-Progress-Export-";

    [ObservableProperty]
    private ObservableCollection<DailyProgress> _allProgressData;

    [ObservableProperty]
    private List<PlotDataPoint> _plotData;

    [ObservableProperty]
    private ObservableCollection<string> _plotTimeRanges;

    [ObservableProperty]
    private string _selectedPlotTimeRange;

    public ObservableCollection<string> ExportSortOrders { get; }

    [ObservableProperty]
    private string _selectedExportSortOrder;

    public ObservableCollection<string> ExportCalendarTypes { get; }

    [ObservableProperty]
    private string _selectedExportCalendarType;

    [ObservableProperty]
    private BarChartDrawable _chartDrawable;

    public StatsViewModel(ProgressStorageService progressStorageService) // Changed
    {
        _progressStorageService = progressStorageService; // Changed
        _allProgressData = [];
        _plotData = [];

        Color initialTextColor = GetCurrentThemeTextColor();
        _chartDrawable = new BarChartDrawable { DataPoints = [], TextColor = initialTextColor };

        PlotTimeRanges =
        [
            "Last 7 Days",
            "Last 30 Days",
            "Last 3 Months",
            "Last 6 Months",
            "Last Year",
            "All Time"
        ];
        SelectedPlotTimeRange = PlotTimeRanges.FirstOrDefault() ?? "Last 7 Days";

        ExportSortOrders =
        [
            "Ascending by Date",
            "Descending by Date"
        ];
        SelectedExportSortOrder = ExportSortOrders.FirstOrDefault() ?? "Ascending by Date";

        ExportCalendarTypes =
        [
            "Gregorian",
            "Persian"
        ];
        SelectedExportCalendarType = ExportCalendarTypes.FirstOrDefault() ?? "Gregorian";

        _ = LoadProgressAsync();
    }

    partial void OnSelectedPlotTimeRangeChanged(string value)
    {
        Debug.WriteLine($"StatsViewModel: Time range changed to '{value}'. Updating plot data.");
        UpdatePlotData();
    }

    [RelayCommand]
    private async Task LoadProgressAsync()
    {
        Debug.WriteLine("StatsViewModel: LoadProgressAsync started.");
        var progress = await _progressStorageService.LoadDailyProgressAsync(); // Use ProgressStorageService
        Debug.WriteLine($"StatsViewModel: Loaded {progress.Count} progress entries.");
        AllProgressData = new ObservableCollection<DailyProgress>(progress.OrderBy(p => p.Date));
        Debug.WriteLine("StatsViewModel: AllProgressData populated and sorted.");
        UpdatePlotData();
    }

    private void UpdatePlotData()
    {
        Debug.WriteLine($"StatsViewModel: UpdatePlotData started for range '{SelectedPlotTimeRange}'.");
        Color currentTextColor = GetCurrentThemeTextColor();

        if (AllProgressData == null || !AllProgressData.Any())
        {
            Debug.WriteLine("StatsViewModel: No progress data available. Clearing plot.");
            PlotData = [];
            ChartDrawable = new BarChartDrawable { DataPoints = PlotData, TextColor = currentTextColor };
            return;
        }

        DateTime startDate = DateTime.MinValue;
        DateTime today = DateTime.Today;

        startDate = SelectedPlotTimeRange switch
        {
            "Last 7 Days" => today.AddDays(-6),
            "Last 30 Days" => today.AddDays(-29),
            "Last 3 Months" => today.AddMonths(-3).AddDays(1),
            "Last 6 Months" => today.AddMonths(-6).AddDays(1),
            "Last Year" => today.AddYears(-1).AddDays(1),
            _ => AllProgressData.Any() ? AllProgressData.Min(p => p.Date) : DateTime.Today,// Handle empty case
        };
        Debug.WriteLine($"StatsViewModel: Date range calculated: {startDate:yyyy-MM-dd} to {today:yyyy-MM-dd}");

        var filteredProgress = AllProgressData
            .Where(p => p.Date.Date >= startDate.Date && p.Date.Date <= today.Date)
            .OrderBy(p => p.Date)
            .ToList();

        Debug.WriteLine($"StatsViewModel: Filtered data count: {filteredProgress.Count}");

        if (SelectedPlotTimeRange == "All Time" && filteredProgress.Count > 90)
        {
            Debug.WriteLine($"StatsViewModel: Plot has {filteredProgress.Count} points for All Time. Consider aggregation for performance/readability.");
            // Future: Implement aggregation logic here if needed
        }

        CalendarSystemType calendarSystem = AppSettings.SelectedCalendarSystem;
        CultureInfo formatCulture = calendarSystem == CalendarSystemType.Persian ? new("fa-IR") : CultureInfo.InvariantCulture;
        string dateFormat = "dd MMM";
        if (SelectedPlotTimeRange == "Last Year" || (SelectedPlotTimeRange == "All Time" && (today - startDate).TotalDays > 365))
        {
            dateFormat = "MMM yy";
        }

        List<PlotDataPoint> newPlotData = [];
        foreach (var progress in filteredProgress)
        {
            string label;
            if (calendarSystem == CalendarSystemType.Persian)
            {
                PersianCalendar pc = new();
                label = $"{pc.GetDayOfMonth(progress.Date):00} {GetPersianAbbreviatedMonthName(pc.GetMonth(progress.Date))}";
                if (dateFormat == "MMM yy")
                {
                    label = $"{GetPersianAbbreviatedMonthName(pc.GetMonth(progress.Date))} {pc.GetYear(progress.Date) % 100:00}";
                }
            }
            else
            {
                label = progress.Date.ToString(dateFormat, formatCulture);
            }
            newPlotData.Add(new PlotDataPoint(label, progress.PercentageCompleted, DetermineBarColor(progress.PercentageCompleted)));
        }
        PlotData = newPlotData;
        Debug.WriteLine($"StatsViewModel: Created new PlotData list with {PlotData.Count} points.");

        var newDrawable = new BarChartDrawable
        {
            DataPoints = PlotData,
            TextColor = currentTextColor
        };
        ChartDrawable = newDrawable;
        Debug.WriteLine("StatsViewModel: Assigned new BarChartDrawable instance to ChartDrawable property. UI should update.");
    }

    private string GetPersianAbbreviatedMonthName(int month)
    {
        string[] names = ["", "فر", "ار", "خر", "تی", "مر", "شه", "مه", "آب", "آذ", "دی", "به", "اس"];
        return (month >= 1 && month <= 12) ? names[month] : "?";
    }


    private Color DetermineBarColor(double percentage)
    {
        if (percentage >= 0.75) return Colors.LimeGreen;
        if (percentage >= 0.40) return Colors.Yellow;
        return Colors.Red;
    }

    [RelayCommand]
    private async Task ExportProgressAsync()
    {
        string temporaryExportPath = string.Empty;
        try
        {
            List<DailyProgress> dailyProgressList = await _progressStorageService.LoadDailyProgressAsync(); // Use ProgressStorageService

            if (dailyProgressList == null || !dailyProgressList.Any())
            {
                await ShowAlertAsync("Export Progress", "No daily progress data found to export.", "OK");
                return;
            }

            IEnumerable<DailyProgress> sortedProgress = SelectedExportSortOrder == "Ascending by Date"
                ? dailyProgressList.OrderBy(p => p.Date)
                : dailyProgressList.OrderByDescending(p => p.Date);

            StringBuilder sb = new();
            sb.AppendLine("Date - Progress"); // Add header
            foreach (var progress in sortedProgress)
            {
                string dateString = SelectedExportCalendarType == "Persian"
                    ? DateUtils.ToPersianDateString(progress.Date)
                    : DateUtils.ToGregorianDateString(progress.Date);
                string percentageString = progress.PercentageCompleted.ToString("P0", CultureInfo.InvariantCulture);
                sb.AppendLine($"{dateString} - {percentageString}");
            }

            string fileContent = sb.ToString();
            if (string.IsNullOrWhiteSpace(fileContent))
            {
                await ShowAlertAsync("Export Progress", "Failed to generate export content.", "OK");
                return;
            }

            CleanUpOldExportFiles(ProgressExportFilePrefix, ".txt");

            string exportFilename = $"{ProgressExportFilePrefix}{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            temporaryExportPath = Path.Combine(FileSystem.CacheDirectory, exportFilename);

            EnsureDirectoryExists(temporaryExportPath);

            await File.WriteAllTextAsync(temporaryExportPath, fileContent);

            LogFileInfo(temporaryExportPath, "Temporary Progress");

            ShareFileRequest request = new()
            {
                Title = "Export Tickly Progress",
                File = new ShareFile(temporaryExportPath, "text/plain")
            };
            await Share.RequestAsync(request);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ExportProgressAsync: Exception: {ex.Message}");
            await ShowAlertAsync("Export Error", $"An unexpected error occurred: {ex.Message}", "OK");
        }
    }

    private void CleanUpOldExportFiles(string prefix, string extension)
    {
        try
        {
            string cacheDir = FileSystem.CacheDirectory;
            string searchPattern = $"{prefix}*{extension}";
            Debug.WriteLine($"CleanUpOldExportFiles: Searching for old files in '{cacheDir}' with pattern '{searchPattern}'");

            if (!Directory.Exists(cacheDir))
            {
                Debug.WriteLine($"CleanUpOldExportFiles: Cache directory not found: {cacheDir}");
                return;
            }

            IEnumerable<string> oldFiles = Directory.EnumerateFiles(cacheDir, searchPattern);
            int count = 0;
            foreach (string file in oldFiles)
            {
                try
                {
                    File.Delete(file);
                    Debug.WriteLine($"CleanUpOldExportFiles: Deleted old file: {file}");
                    count++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"CleanUpOldExportFiles: Error deleting old file '{file}': {ex.Message}");
                }
            }
            Debug.WriteLine($"CleanUpOldExportFiles: Deleted {count} old export files matching pattern '{searchPattern}'.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CleanUpOldExportFiles: Error during cleanup for pattern '{prefix}*{extension}': {ex.Message}");
        }
    }

    private void EnsureDirectoryExists(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private void LogFileInfo(string filePath, string fileDescription)
    {
        try
        {
            FileInfo fileInfo = new(filePath);
            Debug.WriteLine($"LogFileInfo: {fileDescription} file size: {fileInfo.Length} bytes. Path: {filePath}");
            if (fileInfo.Length == 0)
            {
                Debug.WriteLine($"LogFileInfo: WARNING - {fileDescription} file is 0 bytes!");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LogFileInfo: Could not get {fileDescription} file info: {ex.Message}. Path: {filePath}");
        }
    }

    private static async Task ShowAlertAsync(string title, string message, string cancelAction)
    {
        if (!MainThread.IsMainThread)
        {
            await MainThread.InvokeOnMainThreadAsync(() => ShowAlertAsync(title, message, cancelAction));
            return;
        }

        Page? currentPage = GetCurrentPage();

        if (currentPage is not null)
        {
            await currentPage.DisplayAlert(title, message, cancelAction);
        }
        else
        {
            Debug.WriteLine($"ShowAlert: Could not find current page to display alert: {title}");
        }
    }

    private static Page? GetCurrentPage()
    {
        Page? currentPage = Application.Current?.MainPage;

        if (currentPage is Shell shell)
        {
            currentPage = shell.CurrentPage;
        }
        else if (currentPage is NavigationPage navPage)
        {
            currentPage = navPage.CurrentPage;
        }
        else if (currentPage is TabbedPage tabbedPage)
        {
            currentPage = tabbedPage.CurrentPage;
        }

        if (currentPage == null && Application.Current?.Windows is { Count: > 0 } windows && windows[0].Page is not null)
        {
            currentPage = windows[0].Page;
            if (currentPage is NavigationPage navPageModal && navPageModal.CurrentPage != null)
            {
                currentPage = navPageModal.CurrentPage;
            }
        }

        if (currentPage == null)
        {
            Debug.WriteLine("GetCurrentPage: Could not determine the current page reliably.");
        }
        return currentPage;
    }

    private static Color GetCurrentThemeTextColor()
    {
        if (Application.Current != null && Application.Current.Resources.TryGetValue("AppForegroundColor", out var foregroundColor) && foregroundColor is Color color)
        {
            return color;
        }
        return Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.WhiteSmoke : Colors.Black;
    }
}