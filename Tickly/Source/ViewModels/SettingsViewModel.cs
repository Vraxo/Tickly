using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Tickly.Messages;
using Tickly.Models;
using Tickly.Services;
using Tickly.Utils; // Added for DateUtils

namespace Tickly.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly TaskPersistenceService _taskPersistenceService; // Added
    private bool _isGregorianSelected;
    private bool _isPersianSelected;
    private readonly string _applicationTasksFilePath;
    private const string ExportFilePrefix = "Tickly-Tasks-"; // Define prefix for cleanup

    public bool IsGregorianSelected
    {
        get => _isGregorianSelected;
        set
        {
            if (SetProperty(ref _isGregorianSelected, value) && value)
            {
                OnCalendarSelectionChanged(true);
            }
        }
    }

    public bool IsPersianSelected
    {
        get => _isPersianSelected;
        set
        {
            if (SetProperty(ref _isPersianSelected, value) && value)
            {
                OnCalendarSelectionChanged(false);
            }
        }
    }

    public SettingsViewModel(TaskPersistenceService taskPersistenceService) // Injected
    {
        _taskPersistenceService = taskPersistenceService; // Store injected service
        _applicationTasksFilePath = Path.Combine(FileSystem.AppDataDirectory, "tasks.json");
        Debug.WriteLine($"SettingsViewModel: Tasks file path is set to: {_applicationTasksFilePath}");

        LoadSettings();
    }

    private void OnCalendarSelectionChanged(bool isGregorianNowSelected)
    {
        if (isGregorianNowSelected)
        {
            SetProperty(ref _isPersianSelected, false, nameof(IsPersianSelected));
            UpdateCalendarSetting(CalendarSystemType.Gregorian);
        }
        else
        {
            SetProperty(ref _isGregorianSelected, false, nameof(IsGregorianSelected));
            UpdateCalendarSetting(CalendarSystemType.Persian);
        }
    }

    private void UpdateCalendarSetting(CalendarSystemType newSystem)
    {
        if (AppSettings.SelectedCalendarSystem == newSystem)
        {
            return;
        }

        AppSettings.SelectedCalendarSystem = newSystem;
        Preferences.Set(AppSettings.CalendarSystemKey, (int)newSystem);
        WeakReferenceMessenger.Default.Send(new CalendarSettingChangedMessage());
    }

    private void LoadSettings()
    {
        CalendarSystemType currentSystem = AppSettings.SelectedCalendarSystem;

        bool shouldBeGregorian = currentSystem == CalendarSystemType.Gregorian;
        bool shouldBePersian = currentSystem == CalendarSystemType.Persian;

        SetProperty(ref _isGregorianSelected, shouldBeGregorian, nameof(IsGregorianSelected));
        SetProperty(ref _isPersianSelected, shouldBePersian, nameof(IsPersianSelected));

        if (!_isGregorianSelected && !_isPersianSelected)
        {
            SetProperty(ref _isGregorianSelected, true, nameof(IsGregorianSelected));
            AppSettings.SelectedCalendarSystem = CalendarSystemType.Gregorian;
            Preferences.Set(AppSettings.CalendarSystemKey, (int)CalendarSystemType.Gregorian);
        }
    }

    [RelayCommand]
    private async Task ExportTasksAsync()
    {
        string temporaryExportPath = string.Empty; // Keep track for potential logging if needed
        Debug.WriteLine("ExportTasksAsync: Starting export process.");

        try
        {
            Debug.WriteLine($"ExportTasksAsync: Checking existence of source file: {_applicationTasksFilePath}");
            if (!File.Exists(_applicationTasksFilePath))
            {
                Debug.WriteLine("ExportTasksAsync: Source tasks file does not exist.");
                await SettingsViewModel.ShowAlertAsync("Export Failed", "No tasks file exists to export.", "OK");
                return;
            }
            Debug.WriteLine("ExportTasksAsync: Source tasks file found.");

            // *** Step 1: Clean up OLD export files ***
            CleanUpOldExportFiles();

            // Log source file size
            try
            {
                FileInfo sourceInfo = new(_applicationTasksFilePath);
                Debug.WriteLine($"ExportTasksAsync: Source file size: {sourceInfo.Length} bytes.");
                if (sourceInfo.Length == 0)
                {
                    Debug.WriteLine("ExportTasksAsync: WARNING - Source file is 0 bytes.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExportTasksAsync: Could not get source file info: {ex.Message}");
            }

            // *** Step 2: Create the NEW temporary file ***
            string exportFilename = $"{ExportFilePrefix}{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
            temporaryExportPath = Path.Combine(FileSystem.CacheDirectory, exportFilename);
            Debug.WriteLine($"ExportTasksAsync: Temporary export path set to: {temporaryExportPath}");

            string? cacheDir = Path.GetDirectoryName(temporaryExportPath);
            if (cacheDir is not null && !Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
                Debug.WriteLine($"ExportTasksAsync: Created cache directory: {cacheDir}");
            }

            Debug.WriteLine($"ExportTasksAsync: Attempting to copy file from '{_applicationTasksFilePath}' to '{temporaryExportPath}'");
            File.Copy(_applicationTasksFilePath, temporaryExportPath, true);
            Debug.WriteLine("ExportTasksAsync: File copy successful.");

            if (!File.Exists(temporaryExportPath))
            {
                Debug.WriteLine($"ExportTasksAsync: ERROR - Temporary file does not exist after copy attempt: {temporaryExportPath}");
                await SettingsViewModel.ShowAlertAsync("Export Error", "Failed to create temporary export file.", "OK");
                return;
            }
            Debug.WriteLine($"ExportTasksAsync: Temporary file confirmed to exist: {temporaryExportPath}");

            // Log temporary file size
            try
            {
                FileInfo tempInfo = new(temporaryExportPath);
                Debug.WriteLine($"ExportTasksAsync: Temporary file size AFTER copy: {tempInfo.Length} bytes.");
                if (tempInfo.Length == 0)
                {
                    Debug.WriteLine($"ExportTasksAsync: ERROR - Temporary file is 0 bytes after copy!");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExportTasksAsync: Could not get temporary file info: {ex.Message}");
            }

            // *** Step 3: Share the NEW file ***
            ShareFileRequest request = new()
            {
                Title = "Export Tickly Tasks",
                File = new ShareFile(temporaryExportPath, "application/json")
            };
            Debug.WriteLine("ExportTasksAsync: ShareFileRequest created. Title: " + request.Title + ", File Path: " + request.File.FullPath);

            Debug.WriteLine("ExportTasksAsync: Calling Share.RequestAsync...");
            await Share.RequestAsync(request);
            Debug.WriteLine("ExportTasksAsync: Share.RequestAsync completed.");

            // *** Step 4: NO IMMEDIATE DELETION ***
            // The file remains in the cache directory. It will be cleaned up
            // the *next* time the user exports.

        }
        catch (Exception exception)
        {
            Debug.WriteLine($"ExportTasksAsync: Exception caught: {exception.GetType().Name} - {exception.Message}\nStackTrace: {exception.StackTrace}");
            await SettingsViewModel.ShowAlertAsync("Export Error", $"An error occurred during export: {exception.Message}", "OK");
        }
        // The finally block is now removed as we don't delete the current file.
    }

    // *** NEW METHOD for cleanup ***
    private void CleanUpOldExportFiles()
    {
        try
        {
            string cacheDir = FileSystem.CacheDirectory;
            string searchPattern = $"{ExportFilePrefix}*.json";
            Debug.WriteLine($"CleanUpOldExportFiles: Searching for old files in '{cacheDir}' with pattern '{searchPattern}'");

            if (!Directory.Exists(cacheDir))
            {
                Debug.WriteLine($"CleanUpOldExportFiles: Cache directory not found.");
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
                    // Log but continue trying to delete others
                }
            }
            Debug.WriteLine($"CleanUpOldExportFiles: Deleted {count} old export files.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CleanUpOldExportFiles: Error during cleanup: {ex.Message}");
            // Log the error but don't let cleanup failure prevent the export
        }
    }


    [RelayCommand]
    private async Task ImportTasksAsync()
    {
        try
        {
            FilePickerFileType customFileType = new(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    [DevicePlatform.iOS] = ["public.json", "public.text"],
                    [DevicePlatform.Android] = ["application/json", "text/plain"],
                    [DevicePlatform.WinUI] = [".json", ".txt"],
                    [DevicePlatform.MacCatalyst] = ["json", "txt"]
                });

            PickOptions options = new()
            {
                PickerTitle = "Select Tickly tasks JSON file",
                FileTypes = customFileType,
            };

            Debug.WriteLine("ImportTasksAsync: Calling FilePicker.PickAsync...");
            FileResult? result = await FilePicker.PickAsync(options);

            if (result is null)
            {
                Debug.WriteLine("ImportTasksAsync: FilePicker returned null (user cancelled?).");
                return;
            }

            Debug.WriteLine($"ImportTasksAsync: File picked: {result.FullPath}");
            string fileContent;

            try
            {
                Debug.WriteLine($"ImportTasksAsync: Reading file content from: {result.FullPath}");
                fileContent = await File.ReadAllTextAsync(result.FullPath);
                Debug.WriteLine("ImportTasksAsync: File content read successfully. Validating JSON...");
                _ = JsonSerializer.Deserialize<List<TaskItem>>(fileContent);
                Debug.WriteLine("ImportTasksAsync: JSON validation successful.");
            }
            catch (JsonException jsonException)
            {
                Debug.WriteLine($"ImportTasksAsync: Invalid JSON format during import: {jsonException.Message}");
                await SettingsViewModel.ShowAlertAsync("Import Failed", "The selected file is not a valid JSON task file.", "OK");
                return;
            }
            catch (Exception readException)
            {
                Debug.WriteLine($"ImportTasksAsync: Error reading selected file: {readException.GetType().Name} - {readException.Message}");
                await SettingsViewModel.ShowAlertAsync("Import Failed", $"Could not read the selected file: {readException.Message}", "OK");
                return;
            }

            bool confirmed = await ShowConfirmationAsync(
                "Confirm Import",
                "This will REPLACE your current tasks with the content of the selected file. This cannot be undone. Proceed?",
                "Replace",
                "Cancel");

            if (!confirmed)
            {
                Debug.WriteLine("ImportTasksAsync: User cancelled confirmation.");
                return;
            }

            try
            {
                Debug.WriteLine($"ImportTasksAsync: Copying file from '{result.FullPath}' to '{_applicationTasksFilePath}'");
                File.Copy(result.FullPath, _applicationTasksFilePath, true);
                Debug.WriteLine("ImportTasksAsync: File copy successful. Sending reload message.");
                WeakReferenceMessenger.Default.Send(new TasksReloadRequestedMessage());
                await ShowAlertAsync("Import Successful", "Tasks imported successfully. The task list has been updated.", "OK");
            }
            catch (Exception copyException)
            {
                Debug.WriteLine($"ImportTasksAsync: Error copying imported file: {copyException.GetType().Name} - {copyException.Message}");
                await ShowAlertAsync("Import Failed", $"Could not replace the tasks file: {copyException.Message}", "OK");
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"ImportTasksAsync: General error during import: {exception.GetType().Name} - {exception.Message}\nStackTrace: {exception.StackTrace}");
            await ShowAlertAsync("Import Error", $"An unexpected error occurred during import: {exception.Message}", "OK");
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

    // Static helper to display confirmations using the current page context
    private static async Task<bool> ShowConfirmationAsync(string title, string message, string acceptAction, string cancelAction)
    {
        if (!MainThread.IsMainThread)
        {
            return await MainThread.InvokeOnMainThreadAsync(() => ShowConfirmationAsync(title, message, acceptAction, cancelAction));
        }

        Page? currentPage = GetCurrentPage();
        if (currentPage is not null)
        {
            return await currentPage.DisplayAlert(title, message, acceptAction, cancelAction);
        }
        else
        {
            Debug.WriteLine($"ShowConfirmation: Could not find current page to display confirmation: {title}");
            return false; // Assume cancellation if page not found
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
