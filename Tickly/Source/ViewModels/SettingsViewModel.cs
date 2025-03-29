// File: ViewModels/SettingsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input; // Needed for RelayCommand
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Storage;
using System.ComponentModel;
using System.Diagnostics;
using Tickly.Messages;
using Tickly.Models;
using Tickly.Services;
using System.Collections.Generic; // For EqualityComparer
using System.IO; // For Path, File
using System.Text.Json; // For JsonSerializer validation
using System.Threading.Tasks; // For Task
using Microsoft.Maui.ApplicationModel; // For Permissions, Share, FilePicker

namespace Tickly.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isGregorianSelected;

        [ObservableProperty]
        private bool _isPersianSelected;

        // Path to the app's internal tasks file
        private readonly string _appTasksFilePath;

        public SettingsViewModel()
        {
            _appTasksFilePath = Path.Combine(FileSystem.AppDataDirectory, "tasks.json");

            LoadSettings();
            this.PropertyChanged += SettingsViewModel_PropertyChanged;
        }


        private void LoadSettings()
        {
            var currentSystem = AppSettings.SelectedCalendarSystem;
            Debug.WriteLine($"SettingsViewModel: Loading initial state from AppSettings. CurrentSystem='{currentSystem}'");
            UpdateProperty(ref _isGregorianSelected, currentSystem == CalendarSystemType.Gregorian, nameof(IsGregorianSelected));
            UpdateProperty(ref _isPersianSelected, currentSystem == CalendarSystemType.Persian, nameof(IsPersianSelected));
        }

        private void SettingsViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            CalendarSystemType newSystem;

            if (e.PropertyName == nameof(IsGregorianSelected) && IsGregorianSelected)
            {
                newSystem = CalendarSystemType.Gregorian;
                if (IsPersianSelected) UpdateProperty(ref _isPersianSelected, false, nameof(IsPersianSelected));
            }
            else if (e.PropertyName == nameof(IsPersianSelected) && IsPersianSelected)
            {
                newSystem = CalendarSystemType.Persian;
                if (IsGregorianSelected) UpdateProperty(ref _isGregorianSelected, false, nameof(IsGregorianSelected));
            }
            else { return; } // Only handle changes to these specific boolean properties

            // Check if the AppSetting actually needs changing
            if (AppSettings.SelectedCalendarSystem != newSystem)
            {
                Debug.WriteLine($"SettingsViewModel: PropertyChanged detected user change to {newSystem}. Saving and notifying.");
                AppSettings.SelectedCalendarSystem = newSystem;
                Preferences.Set(AppSettings.CalendarSystemKey, (int)newSystem);
                WeakReferenceMessenger.Default.Send(new CalendarSettingChangedMessage());
                Debug.WriteLine("SettingsViewModel: Sent CalendarSettingChangedMessage.");
            }
        }

        // *** NEW Export Command ***
        [RelayCommand]
        private async Task ExportTasksAsync()
        {
            Debug.WriteLine("ExportTasksAsync: Initiating export...");
            try
            {
                if (!File.Exists(_appTasksFilePath))
                {
                    Debug.WriteLine("ExportTasksAsync: No tasks file found to export.");
                    await ShowAlert("Export Failed", "No tasks file exists to export.", "OK");
                    return;
                }

                // Use MAUI Share API to let user choose destination
                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = "Export Tickly Tasks",
                    File = new ShareFile(_appTasksFilePath, "application/json") // Specify MIME type
                });

                Debug.WriteLine("ExportTasksAsync: Share request completed (user may have saved or cancelled).");
                // Optionally show a confirmation, but Share doesn't give explicit success feedback
                // await ShowAlert("Export", "Share/Save dialog opened.", "OK");

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExportTasksAsync: Error during export: {ex.Message}");
                await ShowAlert("Export Error", $"An error occurred during export: {ex.Message}", "OK");
            }
        }
        // *** END Export Command ***

        // *** NEW Import Command ***
        [RelayCommand]
        private async Task ImportTasksAsync()
        {
            Debug.WriteLine("ImportTasksAsync: Initiating import...");
            try
            {
                var customFileType = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "public.json" } }, // UTType
                        { DevicePlatform.Android, new[] { "application/json" } }, // MIME type
                        { DevicePlatform.WinUI, new[] { ".json" } }, // file extension
                        { DevicePlatform.MacCatalyst, new[] { "json" } }, // UTType
                    });

                var options = new PickOptions
                {
                    PickerTitle = "Select Tickly tasks JSON file",
                    FileTypes = customFileType,
                };

                var result = await FilePicker.PickAsync(options);
                if (result == null)
                {
                    Debug.WriteLine("ImportTasksAsync: File picking cancelled by user.");
                    return;
                }

                Debug.WriteLine($"ImportTasksAsync: File picked: {result.FullPath}");

                // --- Basic Validation ---
                string fileContent;
                try
                {
                    fileContent = await File.ReadAllTextAsync(result.FullPath);
                    // Attempt deserialization just to see if it's plausible JSON structure
                    _ = JsonSerializer.Deserialize<List<TaskItem>>(fileContent);
                    Debug.WriteLine("ImportTasksAsync: File content successfully deserialized (basic validation passed).");
                }
                catch (JsonException jsonEx)
                {
                    Debug.WriteLine($"ImportTasksAsync: Invalid JSON format: {jsonEx.Message}");
                    await ShowAlert("Import Failed", "The selected file is not a valid JSON task file.", "OK");
                    return;
                }
                catch (Exception readEx)
                {
                    Debug.WriteLine($"ImportTasksAsync: Error reading selected file: {readEx.Message}");
                    await ShowAlert("Import Failed", $"Could not read the selected file: {readEx.Message}", "OK");
                    return;
                }

                // --- Confirmation ---
                bool confirmed = await ShowConfirmation("Confirm Import", "This will REPLACE your current tasks with the content of the selected file. This cannot be undone. Proceed?", "Replace", "Cancel");

                if (!confirmed)
                {
                    Debug.WriteLine("ImportTasksAsync: Import cancelled by user confirmation.");
                    return;
                }

                // --- Replace File ---
                try
                {
                    File.Copy(result.FullPath, _appTasksFilePath, true); // Overwrite existing file
                    Debug.WriteLine($"ImportTasksAsync: Successfully copied selected file to {_appTasksFilePath}");

                    // --- Trigger Reload ---
                    WeakReferenceMessenger.Default.Send(new TasksReloadRequestedMessage());
                    Debug.WriteLine("ImportTasksAsync: Sent TasksReloadRequestedMessage.");

                    await ShowAlert("Import Successful", "Tasks imported successfully. The task list has been updated.", "OK");
                }
                catch (Exception copyEx)
                {
                    Debug.WriteLine($"ImportTasksAsync: Error copying file: {copyEx.Message}");
                    await ShowAlert("Import Failed", $"Could not replace the tasks file: {copyEx.Message}", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImportTasksAsync: General error during import: {ex.Message}");
                await ShowAlert("Import Error", $"An unexpected error occurred during import: {ex.Message}", "OK");
            }
        }
        // *** END Import Command ***


        // Helper for showing alerts (slight MVVM violation for simplicity)
        private async Task ShowAlert(string title, string message, string cancel)
        {
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert(title, message, cancel);
            }
        }

        // Helper for showing confirmation dialog
        private async Task<bool> ShowConfirmation(string title, string message, string accept, string cancel)
        {
            if (Application.Current?.MainPage != null)
            {
                return await Application.Current.MainPage.DisplayAlert(title, message, accept, cancel);
            }
            return false; // Cannot show dialog
        }


        protected bool UpdateProperty<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}