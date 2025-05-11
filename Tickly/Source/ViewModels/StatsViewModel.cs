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
    private readonly ProgressStorageService _progressStorageService;
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

    public StatsViewModel(ProgressStorageService progressStorageService)
    {
        _progressStorageService = progressStorageService;
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
        UpdatePlotData();
    }

    [RelayCommand]
    private async Task LoadProgressAsync()
    {
        var progress = await _progressStorageService.LoadDailyProgressAsync();
        AllProgressData = new ObservableCollection<DailyProgress>(progress.OrderBy(p => p.Date));
        UpdatePlotData();
    }

    private void UpdatePlotData()
    {
        Color currentTextColor = GetCurrentThemeTextColor();

        if (AllProgressData == null || !AllProgressData.Any())
        {
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
            _ => AllProgressData.Any() ? AllProgressData.Min(p => p.Date) : DateTime.Today,
        };

        var filteredProgress = AllProgressData
            .Where(p => p.Date.Date >= startDate.Date && p.Date.Date <= today.Date)
            .OrderBy(p => p.Date)
            .ToList();

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

        var newDrawable = new BarChartDrawable
        {
            DataPoints = PlotData,
            TextColor = currentTextColor
        };
        ChartDrawable = newDrawable;
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
            List<DailyProgress> dailyProgressList = await _progressStorageService.LoadDailyProgressAsync();

            if (dailyProgressList == null || !dailyProgressList.Any())
            {
                await ShowAlertAsync("Export Progress", "No daily progress data found to export.", "OK");
                return;
            }

            IEnumerable<DailyProgress> sortedProgress = SelectedExportSortOrder == "Ascending by Date"
                ? dailyProgressList.OrderBy(p => p.Date)
                : dailyProgressList.OrderByDescending(p => p.Date);

            StringBuilder sb = new();
            sb.AppendLine("Date - Progress");
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

            ShareFileRequest request = new()
            {
                Title = "Export Tickly Progress",
                File = new ShareFile(temporaryExportPath, "text/plain")
            };
            await Share.RequestAsync(request);
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Export Error", $"An unexpected error occurred: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task ResetProgressAsync()
    {
        bool confirmed = await ShowConfirmationAsync("Reset Progress", "Are you sure you want to delete ALL progress data? This action cannot be undone.", "Delete All", "Cancel");
        if (!confirmed)
        {
            return;
        }

        await _progressStorageService.ClearAllProgressAsync();
        AllProgressData.Clear(); // Clear the local collection
        UpdatePlotData(); // This will now show "No data" or an empty chart
        await ShowAlertAsync("Progress Reset", "All progress data has been cleared.", "OK");
    }

    private void CleanUpOldExportFiles(string prefix, string extension)
    {
        try
        {
            string cacheDir = FileSystem.CacheDirectory;
            if (!Directory.Exists(cacheDir))
            {
                return;
            }

            string searchPattern = $"{prefix}*{extension}";
            IEnumerable<string> oldFiles = Directory.EnumerateFiles(cacheDir, searchPattern);
            foreach (string file in oldFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception)
                {
                    // Log or ignore
                }
            }
        }
        catch (Exception)
        {
            // Log or ignore
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
    }

    private static async Task<bool> ShowConfirmationAsync(string title, string message, string accept, string cancel)
    {
        if (!MainThread.IsMainThread)
        {
            return await MainThread.InvokeOnMainThreadAsync(() => ShowConfirmationAsync(title, message, accept, cancel));
        }

        Page? currentPage = GetCurrentPage();
        if (currentPage is not null)
        {
            return await currentPage.DisplayAlert(title, message, accept, cancel);
        }
        return false;
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