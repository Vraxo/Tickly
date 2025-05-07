using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tickly.Models;
using Tickly.Services;
using Tickly.Utils; // Added for DateUtils
using Microcharts; // Added for Microcharts
using SkiaSharp; // Added for SkiaSharp colors

namespace Tickly.ViewModels;

public sealed partial class ProgressViewModel : ObservableObject
{
    private readonly TaskPersistenceService _taskPersistenceService;

    // Properties for Progress Export
    public ObservableCollection<string> ExportSortOrders { get; }
    [ObservableProperty]
    private string _selectedExportSortOrder;

    public ObservableCollection<string> ExportCalendarTypes { get; }
    [ObservableProperty]
    private string _selectedExportCalendarType;

    // Property for chart data
    [ObservableProperty]
    private LineChart _progressChart;

    public ProgressViewModel(TaskPersistenceService taskPersistenceService)
    {
        _taskPersistenceService = taskPersistenceService;

        // Initialize Progress Export Options
        ExportSortOrders = new ObservableCollection<string>
        {
            "Ascending by Date",
            "Descending by Date"
        };
        _selectedExportSortOrder = ExportSortOrders.FirstOrDefault() ?? "Ascending by Date";

        ExportCalendarTypes = new ObservableCollection<string>
        {
            "Gregorian",
            "Persian"
        };
        _selectedExportCalendarType = ExportCalendarTypes.FirstOrDefault() ?? "Gregorian";

        // Load data for the plot when the ViewModel is created
        LoadProgressDataForPlotAsync();
    }

    // Method to load and prepare data for the plot
    private async Task LoadProgressDataForPlotAsync()
    {
        List<DailyProgress> dailyProgressList = await _taskPersistenceService.LoadDailyProgressAsync();

        if (dailyProgressList == null || !dailyProgressList.Any())
        {
            ProgressChart = null; // Or set to an empty chart/message
            return;
        }

        // Sort data by date ascending for the chart
        var sortedProgress = dailyProgressList.OrderBy(p => p.Date).ToList();

        var entries = new List<ChartEntry>();

        foreach (var progress in sortedProgress)
        {
            // Use Gregorian date for chart labels for consistency, or add a setting later
            string dateLabel = progress.Date.ToString("MM/dd"); // Example format

            entries.Add(new ChartEntry((float)progress.PercentageCompleted * 100) // Convert 0-1 to 0-100
            {
                Label = dateLabel,
                ValueLabel = progress.PercentageCompleted.ToString("P0", CultureInfo.InvariantCulture), // e.g., "75%"
                Color = SKColor.Parse("#266489") // Example color
            });
        }

        ProgressChart = new LineChart
        {
            Entries = entries,
            LineMode = LineMode.Spline,
            PointMode = PointMode.Circle,
            LabelTextSize = 20f,
            LabelColor = SKColor.Parse("#FFFFFF"), // White labels
            ValueLabelOrientation = Orientation.Horizontal,
            LabelOrientation = Orientation.Horizontal,
            ShowYAxisText = true,
            ShowYAxisLines = true,
            LineAreaAlpha = 0, // No fill under the line
            LineSize = 3,
            PointSize = 8,
            BackgroundColor = SKColor.Parse("#000000") // Black background
        };
    }

    [RelayCommand]
    private async Task ExportProgressAsync()
    {
        Debug.WriteLine("ProgressViewModel.ExportProgressAsync: Starting progress export.");
        try
        {
            List<DailyProgress> dailyProgressList = await _taskPersistenceService.LoadDailyProgressAsync();

            if (dailyProgressList == null || !dailyProgressList.Any())
            {
                Debug.WriteLine("ProgressViewModel.ExportProgressAsync: No progress data to export.");
                await ShowAlertAsync("Export Progress", "No daily progress data found to export.", "OK");
                return;
            }

            Debug.WriteLine($"ProgressViewModel.ExportProgressAsync: Loaded {dailyProgressList.Count} progress entries.");

            // Sort
            IEnumerable<DailyProgress> sortedProgress;
            if (SelectedExportSortOrder == "Ascending by Date")
            {
                sortedProgress = dailyProgressList.OrderBy(p => p.Date);
                Debug.WriteLine("ProgressViewModel.ExportProgressAsync: Sorting by date ascending.");
            }
            else
            {
                sortedProgress = dailyProgressList.OrderByDescending(p => p.Date);
                Debug.WriteLine("ProgressViewModel.ExportProgressAsync: Sorting by date descending.");
            }

            // Format
            StringBuilder sb = new();
            Debug.WriteLine($"ProgressViewModel.ExportProgressAsync: Formatting using {SelectedExportCalendarType} calendar.");
            foreach (var progress in sortedProgress)
            {
                string dateString;
                if (SelectedExportCalendarType == "Persian")
                {
                    dateString = DateUtils.ToPersianDateString(progress.Date);
                }
                else
                {
                    dateString = DateUtils.ToGregorianDateString(progress.Date);
                }
                // Format percentage as "XX%" (e.g., 0.75 -> "75%")
                string percentageString = progress.PercentageCompleted.ToString("P0", CultureInfo.InvariantCulture);
                sb.AppendLine($"{dateString}-{percentageString}");
            }

            string fileContent = sb.ToString();
            if (string.IsNullOrWhiteSpace(fileContent))
            {
                Debug.WriteLine("ProgressViewModel.ExportProgressAsync: Formatted content is empty.");
                await ShowAlertAsync("Export Progress", "Failed to generate export content.", "OK");
                return;
            }

            string fileName = $"Tickly-Progress-Export-{DateTime.Now:yyyyMMddHHmmss}.txt";
            Debug.WriteLine($"ProgressViewModel.ExportProgressAsync: Preparing to save file: {fileName}");

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));

            var fileSaverResult = await FileSaver.Default.SaveAsync(fileName, stream, CancellationToken.None);

            if (fileSaverResult.IsSuccessful)
            {
                Debug.WriteLine($"ProgressViewModel.ExportProgressAsync: File saved successfully to: {fileSaverResult.FilePath ?? "N/A"}");
                await ShowAlertAsync("Export Successful", $"Progress data exported successfully to {Path.GetFileName(fileSaverResult.FilePath ?? fileName)}", "OK");
            }
            else
            {
                Debug.WriteLine($"ProgressViewModel.ExportProgressAsync: File save failed. Error: {fileSaverResult.Exception?.Message ?? "Unknown error"}");
                await ShowAlertAsync("Export Failed", $"Could not save the file: {fileSaverResult.Exception?.Message ?? "An unknown error occurred."}", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ProgressViewModel.ExportProgressAsync: Exception caught: {ex.GetType().Name} - {ex.Message}\nStackTrace: {ex.StackTrace}");
            await ShowAlertAsync("Export Error", $"An unexpected error occurred during progress export: {ex.Message}", "OK");
        }
    }

    // Static helper to display alerts using the current page context
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
        if (Application.Current?.MainPage is Shell shell && shell.CurrentPage is not null)
        {
            return shell.CurrentPage;
        }
        if (Application.Current?.MainPage is Page mainPage)
        {
            return mainPage;
        }
        if (Application.Current?.Windows is { Count: > 0 } windows && windows[0].Page is not null)
        {
            return windows[0].Page;
        }
        Debug.WriteLine("GetCurrentPage: Could not determine the current page.");
        return null;
    }
}
