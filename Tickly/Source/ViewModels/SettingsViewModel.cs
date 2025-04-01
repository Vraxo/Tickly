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
    private bool _isGregorianSelected;
    private bool _isPersianSelected;
    private readonly string _applicationTasksFilePath;

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

    public SettingsViewModel()
    {
        _applicationTasksFilePath = Path.Combine(FileSystem.AppDataDirectory, "tasks.json");
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

        try
        {
            if (!File.Exists(_applicationTasksFilePath))
            {
                await SettingsViewModel.ShowAlertAsync("Export Failed", "No tasks file exists to export.", "OK");
                return;
            }

            string exportFilename = $"Tickly-Tasks-{DateTime.Now:yyyy--MM--dd}.json";
            temporaryExportPath = Path.Combine(FileSystem.CacheDirectory, exportFilename);

            File.Copy(_applicationTasksFilePath, temporaryExportPath, true);

            ShareFileRequest request = new()
            {
                Title = "Export Tickly Tasks",
                File = new(temporaryExportPath, "application/json")
            };

            await Share.RequestAsync(request);
        }
        catch (Exception exception)
        {
            await SettingsViewModel.ShowAlertAsync("Export Error", $"An error occurred during export: {exception.Message}", "OK");
        }
        finally
        {
            if (!string.IsNullOrEmpty(temporaryExportPath) && File.Exists(temporaryExportPath))
            {
                try
                {
                    File.Delete(temporaryExportPath);
                }
                catch (Exception cleanupException)
                {
                    Debug.WriteLine($"Error cleaning up temporary file: {cleanupException.Message}");
                }
            }
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
                    [DevicePlatform.iOS] = ["public.json"],
                    [DevicePlatform.Android] = ["application/json"],
                    [DevicePlatform.WinUI] = [".json"],
                    [DevicePlatform.MacCatalyst] = ["json"]
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
                await SettingsViewModel.ShowAlertAsync("Import Failed", "The selected file is not a valid JSON task file.", "OK");
                Debug.WriteLine($"Invalid JSON format during import: {jsonException.Message}");
                return;
            }
            catch (Exception readException)
            {
                await SettingsViewModel.ShowAlertAsync("Import Failed", $"Could not read the selected file: {readException.Message}", "OK");
                Debug.WriteLine($"Error reading selected file for import: {readException.Message}");
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
                File.Copy(result.FullPath, _applicationTasksFilePath, true);
                WeakReferenceMessenger.Default.Send(new TasksReloadRequestedMessage());
                await SettingsViewModel.ShowAlertAsync("Import Successful", "Tasks imported successfully. The task list has been updated.", "OK");
            }
            catch (Exception copyException)
            {
                await SettingsViewModel.ShowAlertAsync("Import Failed", $"Could not replace the tasks file: {copyException.Message}", "OK");
                Debug.WriteLine($"Error copying imported file: {copyException.Message}");
            }
        }
        catch (Exception exception)
        {
            await SettingsViewModel.ShowAlertAsync("Import Error", $"An unexpected error occurred during import: {exception.Message}", "OK");
            Debug.WriteLine($"General error during import: {exception.Message}");
        }
    }

    private static async Task ShowAlertAsync(string title, string message, string cancelAction)
    {
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
        if (Shell.Current is not null)
        {
            return Shell.Current.CurrentPage;
        }

        if (Application.Current?.Windows is not { Count: > 0 } windows)
        {
            return null;
        }

        foreach (Window window in windows)
        {
            if (window.Page is null)
            {
                continue;
            }

            return window.Page;
        }

        return null;
    }
}