using System.Diagnostics;
using System.Text.Json;
using Tickly.Models;

namespace Tickly.Services;

public sealed class DataExportService
{
    private readonly TaskStorageService _taskStorageService;
    private readonly ProgressStorageService _progressStorageService;

    public DataExportService(TaskStorageService taskStorageService, ProgressStorageService progressStorageService)
    {
        _taskStorageService = taskStorageService;
        _progressStorageService = progressStorageService;
    }

    public async Task<bool> ExportDataAsync()
    {
        try
        {
            Debug.WriteLine("DataExportService.ExportDataAsync: Initiating export.");
            List<TaskItem> tasks = await _taskStorageService.LoadTasksAsync();
            List<DailyProgress> progress = await _progressStorageService.LoadDailyProgressAsync();

            TicklyDataBundle dataBundle = new()
            {
                Tasks = tasks,
                SelectedCalendarSystem = AppSettings.SelectedCalendarSystem,
                SelectedTheme = AppSettings.SelectedTheme,
                Progress = progress
            };

            JsonSerializerOptions options = new() { WriteIndented = true };
            string json = JsonSerializer.Serialize(dataBundle, options);

            string fileName = $"Tickly_{DateTime.Now:yyyyMMddHHmmss}.json";
            string tempPath = Path.Combine(FileSystem.CacheDirectory, fileName);

            EnsureDirectoryExists(tempPath);
            await File.WriteAllTextAsync(tempPath, json);
            Debug.WriteLine($"DataExportService.ExportDataAsync: Data saved to cache: {tempPath}.");

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Share Tickly Data",
                File = new ShareFile(tempPath)
            });

            Debug.WriteLine("DataExportService.ExportDataAsync: Share request initiated.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DataExportService.ExportDataAsync: Error during export: {ex.Message}");
            // Potentially log more details: ex.StackTrace
            return false;
        }
    }

    private void EnsureDirectoryExists(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            Debug.WriteLine($"DataExportService: Created directory: {directory}");
        }
    }
}