using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Tickly.Messages;
using Tickly.Models;
using Tickly.Services;

namespace Tickly.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly string applicationTasksFilePath;
    private const string ExportFilePrefix = "Tickly-Tasks-";

    public bool IsGregorianSelected
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnIsGregorianSelectedChanged(value);
            }
        }
    }

    public bool IsPersianSelected
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnIsPersianSelectedChanged(value);
            }
        }
    }

    public SettingsViewModel()
    {
        applicationTasksFilePath = Path.Combine(FileSystem.AppDataDirectory, "tasks.json");
        Debug.WriteLine($"SettingsViewModel: Tasks file path is set to: {applicationTasksFilePath}");
        LoadSettings();
    }

    [RelayCommand]
    private async Task ExportTasksAsync()
    {
        try
        {
            if (!File.Exists(applicationTasksFilePath))
            {
                await ShowAlertAsync("Export Failed", "No tasks file exists to export.", "OK");
                return;
            }

            CleanUpOldExportFiles();

            string exportFilename = $"{ExportFilePrefix}{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
            string temporaryExportPath = Path.Combine(FileSystem.CacheDirectory, exportFilename);

            string? cacheDir = Path.GetDirectoryName(temporaryExportPath);
            if (cacheDir is not null && !Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }

            File.Copy(applicationTasksFilePath, temporaryExportPath, true);

            if (!File.Exists(temporaryExportPath))
            {
                Debug.WriteLine($"ExportTasksAsync: ERROR - Temporary file does not exist after copy attempt: {temporaryExportPath}");
                await ShowAlertAsync("Export Error", "Failed to create temporary export file.", "OK");
                return;
            }

            try
            {
                FileInfo tempInfo = new(temporaryExportPath);
                if (tempInfo.Length == 0)
                {
                    Debug.WriteLine($"ExportTasksAsync: ERROR - Temporary file is 0 bytes after copy!");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExportTasksAsync: Could not get temporary file info: {ex.Message}");
            }

            ShareFileRequest request = new()
            {
                Title = "Export Tickly Tasks",
                File = new(temporaryExportPath, "application/json")
            };

            await Share.RequestAsync(request);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"ExportTasksAsync: Exception caught: {exception.GetType().Name} - {exception.Message}\nStackTrace: {exception.StackTrace}");
            await ShowAlertAsync("Export Error", $"An error occurred during export: {exception.Message}", "OK");
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

            FileResult? result = await FilePicker.PickAsync(options);

            if (result is null)
            {
                return;
            }

            string fileContent;

            try
            {
                fileContent = await File.ReadAllTextAsync(result.FullPath);
                _ = JsonSerializer.Deserialize<List<TaskItem>>(fileContent);
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
                return;
            }

            try
            {
                File.Copy(result.FullPath, applicationTasksFilePath, true);
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

    private void OnCalendarSelectionChanged(bool isGregorianNowSelected)
    {
        if (isGregorianNowSelected)
        {
            IsPersianSelected = false;
            UpdateCalendarSetting(CalendarSystemType.Gregorian);
        }
        else
        {
            IsGregorianSelected = false;
            UpdateCalendarSetting(CalendarSystemType.Persian);
        }
    }

    private static void UpdateCalendarSetting(CalendarSystemType newSystem)
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

        IsGregorianSelected = currentSystem == CalendarSystemType.Gregorian;
        IsPersianSelected = currentSystem == CalendarSystemType.Persian;

        if (!IsGregorianSelected && !IsPersianSelected)
        {
            IsGregorianSelected = true;
        }
    }

    private static void CleanUpOldExportFiles()
    {
        try
        {
            string cacheDir = FileSystem.CacheDirectory;
            string searchPattern = $"{ExportFilePrefix}*.json";

            if (!Directory.Exists(cacheDir))
            {
                return;
            }

            IEnumerable<string> oldFiles = Directory.EnumerateFiles(cacheDir, searchPattern);
            int count = 0;

            foreach (string file in oldFiles)
            {
                try
                {
                    File.Delete(file);
                    count++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"CleanUpOldExportFiles: Error deleting old file '{file}': {ex.Message}");
                }
            }
            if (count > 0)
            {
                Debug.WriteLine($"CleanUpOldExportFiles: Deleted {count} old export files.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CleanUpOldExportFiles: Error during cleanup: {ex.Message}");
        }
    }

    private void OnIsGregorianSelectedChanged(bool value)
    {
        if (!value)
        {
            return;
        }

        OnCalendarSelectionChanged(true);
    }

    private void OnIsPersianSelectedChanged(bool value)
    {
        if (!value)
        {
            return;
        }

        OnCalendarSelectionChanged(false);
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
        if (Application.Current?.Windows is { Count: > 0 } windows)
        {
            Page? mainPage = windows[0].Page;

            if (mainPage is Shell shell && shell.CurrentPage is not null)
            {
                return shell.CurrentPage;
            }
            else if (mainPage is not null)
            {
                return mainPage;
            }
        }

        Debug.WriteLine("GetCurrentPage: Could not determine the current page using the Windows collection.");
        return null;
    }
}