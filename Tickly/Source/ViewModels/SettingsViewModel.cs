using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Tickly.Messages;
using Tickly.Models;
using Tickly.Services;
using Tickly.Utils;
using Tickly.Views; // Needed for GetCurrentPage

namespace Tickly.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly TaskPersistenceService _taskPersistenceService;
    private bool _isGregorianSelected;
    private bool _isPersianSelected;
    private readonly string _applicationTasksFilePath;
    private const string TaskExportFilePrefix = "Tickly-Tasks-Export-";

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

    public SettingsViewModel(TaskPersistenceService taskPersistenceService)
    {
        _taskPersistenceService = taskPersistenceService;
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
        string temporaryExportPath = string.Empty;
        Debug.WriteLine("ExportTasksAsync: Starting task export process.");

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

            CleanUpOldExportFiles(TaskExportFilePrefix, ".json");

            LogFileInfo(_applicationTasksFilePath, "Source Task");

            string exportFilename = $"{TaskExportFilePrefix}{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
            temporaryExportPath = Path.Combine(FileSystem.CacheDirectory, exportFilename);
            Debug.WriteLine($"ExportTasksAsync: Temporary task export path set to: {temporaryExportPath}");

            EnsureDirectoryExists(temporaryExportPath);

            Debug.WriteLine($"ExportTasksAsync: Attempting to copy task file from '{_applicationTasksFilePath}' to '{temporaryExportPath}'");
            File.Copy(_applicationTasksFilePath, temporaryExportPath, true);
            Debug.WriteLine("ExportTasksAsync: Task file copy successful.");

            if (!File.Exists(temporaryExportPath))
            {
                Debug.WriteLine($"ExportTasksAsync: ERROR - Temporary task file does not exist after copy attempt: {temporaryExportPath}");
                await SettingsViewModel.ShowAlertAsync("Export Error", "Failed to create temporary export file.", "OK");
                return;
            }
            LogFileInfo(temporaryExportPath, "Temporary Task");

            ShareFileRequest request = new()
            {
                Title = "Export Tickly Tasks",
                File = new ShareFile(temporaryExportPath, "application/json")
            };
            Debug.WriteLine("ExportTasksAsync: ShareFileRequest created for tasks. Title: " + request.Title + ", File Path: " + request.File.FullPath);

            Debug.WriteLine("ExportTasksAsync: Calling Share.RequestAsync for tasks...");
            await Share.RequestAsync(request);
            Debug.WriteLine("ExportTasksAsync: Share.RequestAsync for tasks completed.");

        }
        catch (Exception exception)
        {
            Debug.WriteLine($"ExportTasksAsync: Exception caught: {exception.GetType().Name} - {exception.Message}\nStackTrace: {exception.StackTrace}");
            await SettingsViewModel.ShowAlertAsync("Export Error", $"An error occurred during task export: {exception.Message}", "OK");
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

            Debug.WriteLine($"ImportTasksAsync: File picked: {result.FileName} ({result.FullPath})");
            string fileContent;

            try
            {
                Debug.WriteLine($"ImportTasksAsync: Reading file content from: {result.FullPath}");
                using var stream = await result.OpenReadAsync();
                using var reader = new StreamReader(stream);
                fileContent = await reader.ReadToEndAsync();

                Debug.WriteLine($"ImportTasksAsync: File content read successfully ({fileContent.Length} chars). Validating JSON...");
                if (string.IsNullOrWhiteSpace(fileContent))
                {
                    throw new JsonException("Imported file content is empty or whitespace.");
                }

                var validationList = JsonSerializer.Deserialize<List<TaskItem>>(fileContent);
                if (validationList == null)
                {
                    throw new JsonException("Deserialization resulted in null list.");
                }
                Debug.WriteLine($"ImportTasksAsync: JSON validation successful ({validationList.Count} tasks potentially found).");
            }
            catch (JsonException jsonException)
            {
                Debug.WriteLine($"ImportTasksAsync: Invalid JSON format during import validation: {jsonException.Message}");
                await SettingsViewModel.ShowAlertAsync("Import Failed", "The selected file does not contain valid Tickly task data.", "OK");
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
                Debug.WriteLine($"ImportTasksAsync: Writing imported content to '{_applicationTasksFilePath}'");
                await File.WriteAllTextAsync(_applicationTasksFilePath, fileContent);
                Debug.WriteLine("ImportTasksAsync: File write successful. Sending reload message.");
                WeakReferenceMessenger.Default.Send(new TasksReloadRequestedMessage());
                await ShowAlertAsync("Import Successful", "Tasks imported successfully. The task list has been updated.", "OK");
            }
            catch (Exception writeException)
            {
                Debug.WriteLine($"ImportTasksAsync: Error writing imported file: {writeException.GetType().Name} - {writeException.Message}");
                await ShowAlertAsync("Import Failed", $"Could not replace the tasks file: {writeException.Message}", "OK");
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"ImportTasksAsync: General error during import: {exception.GetType().Name} - {exception.Message}\nStackTrace: {exception.StackTrace}");
            await ShowAlertAsync("Import Error", $"An unexpected error occurred during import: {exception.Message}", "OK");
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
            return false;
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