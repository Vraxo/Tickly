using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Tickly.Messages;
using Tickly.Models;

namespace Tickly.Services;

public sealed class DataImportService(TaskStorageService taskStorageService, ProgressStorageService progressStorageService)
{
    private readonly TaskStorageService _taskStorageService = taskStorageService;
    private readonly ProgressStorageService _progressStorageService = progressStorageService;

    public async Task<bool> ImportDataAsync()
    {
        try
        {
            Debug.WriteLine("DataImportService.ImportDataAsync: Initiating import.");
            FileResult? fileResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Pick Tickly Data File",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, ["public.json"] },
                    { DevicePlatform.Android, ["application/json"] },
                    { DevicePlatform.WinUI, [".json"] },
                    { DevicePlatform.macOS, ["json"] }, // covers .json on macOS
                    { DevicePlatform.Tizen, ["*/*"] }, // Generic fallback if needed
                    { DevicePlatform.MacCatalyst, ["public.json"] }, // Use UTType
                    { DevicePlatform.Unknown, [".json"] } // Generic fallback
                })
            });


            if (fileResult == null)
            {
                Debug.WriteLine("DataImportService.ImportDataAsync: No file selected.");
                return false;
            }

            Debug.WriteLine($"DataImportService.ImportDataAsync: File picked: {fileResult.FullPath}");
            string json = await File.ReadAllTextAsync(fileResult.FullPath);

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.WriteLine("DataImportService.ImportDataAsync: Selected file is empty.");
                return false;
            }

            TicklyDataBundle? dataBundle = JsonSerializer.Deserialize<TicklyDataBundle>(json);

            if (dataBundle == null)
            {
                Debug.WriteLine("DataImportService.ImportDataAsync: Failed to deserialize data bundle.");
                return false;
            }

            // Save imported tasks (or clear if null)
            await _taskStorageService.SaveTasksAsync(dataBundle.Tasks ?? []);
            Debug.WriteLine(dataBundle.Tasks != null
                ? "DataImportService.ImportDataAsync: Tasks imported and saved."
                : "DataImportService.ImportDataAsync: Task list in bundle was null, cleared existing tasks.");

            // Overwrite progress data (or clear if null)
            await OverwriteProgressData(dataBundle.Progress ?? []);
            Debug.WriteLine(dataBundle.Progress != null
               ? "DataImportService.ImportDataAsync: Progress imported and saved."
               : "DataImportService.ImportDataAsync: Progress list in bundle was null, cleared existing progress.");


            // Apply settings and notify
            AppSettings.SelectedCalendarSystem = dataBundle.SelectedCalendarSystem;
            Preferences.Set(AppSettings.CalendarSystemKey, (int)dataBundle.SelectedCalendarSystem);
            WeakReferenceMessenger.Default.Send(new CalendarSettingsChangedMessage(dataBundle.SelectedCalendarSystem));
            Debug.WriteLine($"DataImportService.ImportDataAsync: Calendar system imported and set to {dataBundle.SelectedCalendarSystem}.");

            AppSettings.SelectedTheme = dataBundle.SelectedTheme;
            Preferences.Set(AppSettings.ThemePreferenceKey, (int)dataBundle.SelectedTheme);
            WeakReferenceMessenger.Default.Send(new ThemeChangedMessage(dataBundle.SelectedTheme));
            Debug.WriteLine($"DataImportService.ImportDataAsync: Theme imported and set to {dataBundle.SelectedTheme}.");

            // Notify UI to reload tasks
            WeakReferenceMessenger.Default.Send(new TasksReloadRequestedMessage());
            Debug.WriteLine("DataImportService.ImportDataAsync: TasksReloadRequestedMessage sent.");

            Debug.WriteLine("DataImportService.ImportDataAsync: Import successful.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DataImportService.ImportDataAsync: Error during import: {ex.Message}");
            // Potentially log more details: ex.StackTrace
            return false;
        }
    }

    private async Task OverwriteProgressData(List<DailyProgress> progress)
    {
        // Directly use the internal save method from ProgressStorageService
        // Ensure ProgressStorageService.SaveDailyProgressListAsync is accessible (e.g., internal)
        await _progressStorageService.SaveDailyProgressListAsync(progress);
        Debug.WriteLine($"DataImportService.OverwriteProgressData: Saved {progress.Count} progress entries.");
    }
}