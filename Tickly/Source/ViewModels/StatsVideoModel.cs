using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Tickly.Models;
using Tickly.Services;
using Tickly.Utils;
using Tickly.Views; // Needed for GetCurrentPage

namespace Tickly.ViewModels;

public sealed partial class StatsViewModel : ObservableObject
{
    private readonly TaskPersistenceService _taskPersistenceService;
    private const string ProgressExportFilePrefix = "Tickly-Progress-Export-";

    public ObservableCollection<string> ExportSortOrders { get; }

    [ObservableProperty]
    private string _selectedExportSortOrder;

    public ObservableCollection<string> ExportCalendarTypes { get; }

    [ObservableProperty]
    private string _selectedExportCalendarType;

    public StatsViewModel(TaskPersistenceService taskPersistenceService)
    {
        _taskPersistenceService = taskPersistenceService;

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

    }

    [RelayCommand]
    private async Task ExportProgressAsync()
    {
        Debug.WriteLine("ExportProgressAsync: Starting progress export process.");
        string temporaryExportPath = string.Empty;

        try
        {
            List<DailyProgress> dailyProgressList = await _taskPersistenceService.LoadDailyProgressAsync();

            if (dailyProgressList == null || !dailyProgressList.Any())
            {
                Debug.WriteLine("ExportProgressAsync: No progress data to export.");
                await StatsViewModel.ShowAlertAsync("Export Progress", "No daily progress data found to export.", "OK");
                return;
            }
            Debug.WriteLine($"ExportProgressAsync: Loaded {dailyProgressList.Count} progress entries.");

            IEnumerable<DailyProgress> sortedProgress;
            if (SelectedExportSortOrder == "Ascending by Date")
            {
                sortedProgress = dailyProgressList.OrderBy(p => p.Date);
                Debug.WriteLine("ExportProgressAsync: Sorting progress by date ascending.");
            }
            else
            {
                sortedProgress = dailyProgressList.OrderByDescending(p => p.Date);
                Debug.WriteLine("ExportProgressAsync: Sorting progress by date descending.");
            }

            StringBuilder sb = new();
            Debug.WriteLine($"ExportProgressAsync: Formatting progress using {SelectedExportCalendarType} calendar.");
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
                string percentageString = progress.PercentageCompleted.ToString("P0", CultureInfo.InvariantCulture);
                sb.AppendLine($"{dateString}-{percentageString}");
            }

            string fileContent = sb.ToString();
            if (string.IsNullOrWhiteSpace(fileContent))
            {
                Debug.WriteLine("ExportProgressAsync: Formatted content is empty.");
                await StatsViewModel.ShowAlertAsync("Export Progress", "Failed to generate export content.", "OK");
                return;
            }

            CleanUpOldExportFiles(ProgressExportFilePrefix, ".txt");

            string exportFilename = $"{ProgressExportFilePrefix}{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            temporaryExportPath = Path.Combine(FileSystem.CacheDirectory, exportFilename);
            Debug.WriteLine($"ExportProgressAsync: Temporary progress export path set to: {temporaryExportPath}");

            EnsureDirectoryExists(temporaryExportPath);

            Debug.WriteLine($"ExportProgressAsync: Writing progress content ({fileContent.Length} chars) to '{temporaryExportPath}'");
            await File.WriteAllTextAsync(temporaryExportPath, fileContent);
            Debug.WriteLine("ExportProgressAsync: Progress file write successful.");

            if (!File.Exists(temporaryExportPath))
            {
                Debug.WriteLine($"ExportProgressAsync: ERROR - Temporary progress file does not exist after write attempt: {temporaryExportPath}");
                await StatsViewModel.ShowAlertAsync("Export Error", "Failed to create temporary export file.", "OK");
                return;
            }
            LogFileInfo(temporaryExportPath, "Temporary Progress");

            ShareFileRequest request = new()
            {
                Title = "Export Tickly Progress",
                File = new ShareFile(temporaryExportPath, "text/plain")
            };
            Debug.WriteLine("ExportProgressAsync: ShareFileRequest created for progress. Title: " + request.Title + ", File Path: " + request.File.FullPath);

            Debug.WriteLine("ExportProgressAsync: Calling Share.RequestAsync for progress...");
            await Share.RequestAsync(request);
            Debug.WriteLine("ExportProgressAsync: Share.RequestAsync for progress completed.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ExportProgressAsync: Exception caught: {ex.GetType().Name} - {ex.Message}\nStackTrace: {ex.StackTrace}");
            await StatsViewModel.ShowAlertAsync("Export Error", $"An unexpected error occurred during progress export: {ex.Message}", "OK");
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
                catch (IOException ioEx)
                {
                    Debug.WriteLine($"CleanUpOldExportFiles: IO Error deleting old file '{file}': {ioEx.Message}");
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    Debug.WriteLine($"CleanUpOldExportFiles: Access Error deleting old file '{file}': {uaEx.Message}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"CleanUpOldExportFiles: General Error deleting old file '{file}': {ex.Message}");
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
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            Debug.WriteLine($"EnsureDirectoryExists: Created directory: {directory}");
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
        if (Application.Current?.MainPage is Shell shell && shell.CurrentPage is not null)
        {
            return shell.CurrentPage;
        }
        if (Application.Current?.MainPage is Page mainPage)
        {
            if (mainPage is NavigationPage navPage && navPage.CurrentPage != null)
            {
                return navPage.CurrentPage;
            }
            if (mainPage is TabbedPage tabbedPage && tabbedPage.CurrentPage != null)
            {
                return tabbedPage.CurrentPage;
            }
            return mainPage;
        }
        if (Application.Current?.Windows is { Count: > 0 } windows && windows[0].Page is not null)
        {
            if (windows[0].Page is NavigationPage navPage && navPage.CurrentPage != null)
            {
                return navPage.CurrentPage;
            }
            return windows[0].Page;
        }
        Debug.WriteLine("GetCurrentPage: Could not determine the current page reliably.");
        return null;
    }
}