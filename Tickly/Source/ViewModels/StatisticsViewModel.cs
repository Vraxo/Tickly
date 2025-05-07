using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tickly.Models;
using Tickly.Services;
using Tickly.Utils;
using Tickly.Views.Plotting;

namespace Tickly.ViewModels;

public sealed partial class StatisticsViewModel : ObservableObject
{
    private readonly TaskPersistenceService _taskPersistenceService;

    [ObservableProperty]
    private ObservableCollection<DailyProgress> _allProgressData;

    [ObservableProperty]
    private List<PlotDataPoint> _plotData; // Keep for potential direct use if needed elsewhere

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

    public StatisticsViewModel(TaskPersistenceService taskPersistenceService)
    {
        _taskPersistenceService = taskPersistenceService;
        _allProgressData = [];
        _plotData = [];
        // Initialize with an empty drawable initially
        _chartDrawable = new BarChartDrawable { DataPoints = [], TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.WhiteSmoke : Colors.Black };

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

        LoadProgressCommand.Execute(null);
    }

    partial void OnSelectedPlotTimeRangeChanged(string value)
    {
        Debug.WriteLine($"StatisticsViewModel: Time range changed to '{value}'. Updating plot data.");
        UpdatePlotData();
    }

    [RelayCommand]
    private async Task LoadProgressAsync()
    {
        Debug.WriteLine("StatisticsViewModel: LoadProgressAsync started.");
        var progress = await _taskPersistenceService.LoadDailyProgressAsync();
        Debug.WriteLine($"StatisticsViewModel: Loaded {progress.Count} progress entries.");
        AllProgressData = new ObservableCollection<DailyProgress>(progress.OrderBy(p => p.Date));
        Debug.WriteLine("StatisticsViewModel: AllProgressData populated and sorted.");
        UpdatePlotData();
    }

    private void UpdatePlotData()
    {
        Debug.WriteLine($"StatisticsViewModel: UpdatePlotData started for range '{SelectedPlotTimeRange}'.");
        if (AllProgressData == null || !AllProgressData.Any())
        {
            Debug.WriteLine("StatisticsViewModel: No progress data available. Clearing plot.");
            PlotData = [];
            ChartDrawable = new BarChartDrawable { DataPoints = PlotData, TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.WhiteSmoke : Colors.Black };
            // OnPropertyChanged(nameof(ChartDrawable)); // Handled by [ObservableProperty] setter
            return;
        }

        DateTime startDate = DateTime.MinValue;
        DateTime today = DateTime.Today;

        switch (SelectedPlotTimeRange)
        {
            case "Last 7 Days":
                startDate = today.AddDays(-6);
                break;
            case "Last 30 Days":
                startDate = today.AddDays(-29);
                break;
            case "Last 3 Months":
                startDate = today.AddMonths(-3).AddDays(1); // Approx
                break;
            case "Last 6 Months":
                startDate = today.AddMonths(-6).AddDays(1); // Approx
                break;
            case "Last Year":
                startDate = today.AddYears(-1).AddDays(1); // Approx
                break;
            case "All Time":
            default:
                startDate = AllProgressData.Min(p => p.Date);
                break;
        }
        Debug.WriteLine($"StatisticsViewModel: Date range calculated: {startDate:yyyy-MM-dd} to {today:yyyy-MM-dd}");

        var filteredProgress = AllProgressData
            .Where(p => p.Date.Date >= startDate.Date && p.Date.Date <= today.Date)
            .OrderBy(p => p.Date)
            .ToList();

        Debug.WriteLine($"StatisticsViewModel: Filtered data count: {filteredProgress.Count}");

        // Simple downsampling for "All Time" if too many points, e.g., show weekly/monthly averages
        if (SelectedPlotTimeRange == "All Time" && filteredProgress.Count > 90) // Example threshold
        {
            Debug.WriteLine($"StatisticsViewModel: Plot has {filteredProgress.Count} points for All Time. Consider aggregation for performance/readability.");
        }


        CalendarSystemType calendarSystem = AppSettings.SelectedCalendarSystem;
        CultureInfo formatCulture = calendarSystem == CalendarSystemType.Persian ? new("fa-IR") : CultureInfo.InvariantCulture;
        string dateFormat = "dd MMM";
        if (SelectedPlotTimeRange == "Last Year" || (SelectedPlotTimeRange == "All Time" && (today - startDate).TotalDays > 365))
        {
            dateFormat = "MMM yy"; // Use month/year for longer ranges
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
        PlotData = newPlotData; // Update the backing list (optional if not used elsewhere)
        Debug.WriteLine($"StatisticsViewModel: Created new PlotData list with {PlotData.Count} points.");

        // *** Create a new Drawable instance ***
        var newDrawable = new BarChartDrawable
        {
            DataPoints = PlotData,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.WhiteSmoke : Colors.Black
        };
        ChartDrawable = newDrawable; // Assign the new instance, triggers OnPropertyChanged
        Debug.WriteLine("StatisticsViewModel: Assigned new BarChartDrawable instance to ChartDrawable property. UI should update.");

        // OnPropertyChanged(nameof(ChartDrawable)); // No longer needed explicitly due to [ObservableProperty]
    }

    private string GetPersianAbbreviatedMonthName(int month)
    {
        string[] names = ["", "فر", "ار", "خر", "تی", "مر", "شه", "مه", "آب", "آذ", "دی", "به", "اس"];
        return (month >= 1 && month <= 12) ? names[month] : "?";
    }


    private Color DetermineBarColor(double percentage)
    {
        // Simple Green-Yellow-Red gradient
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
            List<DailyProgress> dailyProgressList = await _taskPersistenceService.LoadDailyProgressAsync();

            if (dailyProgressList == null || !dailyProgressList.Any())
            {
                await ShowAlertAsync("Export Progress", "No daily progress data found to export.", "OK");
                return;
            }

            IEnumerable<DailyProgress> sortedProgress = SelectedExportSortOrder == "Ascending by Date"
                ? dailyProgressList.OrderBy(p => p.Date)
                : dailyProgressList.OrderByDescending(p => p.Date);

            StringBuilder sb = new();
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

            string exportFilename = $"Tickly-Progress-Export-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            temporaryExportPath = Path.Combine(FileSystem.CacheDirectory, exportFilename);

            string? directory = Path.GetDirectoryName(temporaryExportPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

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
            Debug.WriteLine($"ExportProgressAsync: Exception: {ex.Message}");
            await ShowAlertAsync("Export Error", $"An unexpected error occurred: {ex.Message}", "OK");
        }
    }

    private static async Task ShowAlertAsync(string title, string message, string cancelAction)
    {
        // Use Application.Current.Dispatcher to ensure UI operations run on the main thread
        if (Application.Current?.Dispatcher != null)
        {
            await Application.Current.Dispatcher.DispatchAsync(async () =>
            {
                Page? currentPage = Application.Current?.MainPage;
                if (currentPage is Shell shell)
                {
                    currentPage = shell.CurrentPage;
                }
                await (currentPage?.DisplayAlert(title, message, cancelAction) ?? Task.CompletedTask);
            });
        }
        else
        {
            Debug.WriteLine($"ShowAlertAsync: Application.Current or Dispatcher is null. Cannot display alert '{title}'.");
        }
    }
}