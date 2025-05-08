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
using Tickly.Views;

namespace Tickly.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly DataExportService _dataExportService;
    private readonly DataImportService _dataImportService;
    private bool _isGregorianSelected;
    private bool _isPersianSelected;
    private bool _isPitchBlackSelected;
    private bool _isDarkGraySelected;
    private bool _isNordSelected;
    private bool _isCatppuccinMochaSelected; // Added
    private bool _isLightSelected;
    private const string OldTaskExportFilePrefix = "Tickly-Tasks-Export-";
    private const string NewDataExportFilePrefix = "Tickly_";

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

    public bool IsPitchBlackSelected
    {
        get => _isPitchBlackSelected;
        set
        {
            if (SetProperty(ref _isPitchBlackSelected, value) && value)
            {
                OnThemeSelectionChanged(ThemeType.PitchBlack);
            }
        }
    }

    public bool IsDarkGraySelected
    {
        get => _isDarkGraySelected;
        set
        {
            if (SetProperty(ref _isDarkGraySelected, value) && value)
            {
                OnThemeSelectionChanged(ThemeType.DarkGray);
            }
        }
    }

    public bool IsNordSelected
    {
        get => _isNordSelected;
        set
        {
            if (SetProperty(ref _isNordSelected, value) && value)
            {
                OnThemeSelectionChanged(ThemeType.Nord);
            }
        }
    }

    // Added Catppuccin Property
    public bool IsCatppuccinMochaSelected
    {
        get => _isCatppuccinMochaSelected;
        set
        {
            if (SetProperty(ref _isCatppuccinMochaSelected, value) && value)
            {
                OnThemeSelectionChanged(ThemeType.CatppuccinMocha);
            }
        }
    }

    public bool IsLightSelected
    {
        get => _isLightSelected;
        set
        {
            if (SetProperty(ref _isLightSelected, value) && value)
            {
                OnThemeSelectionChanged(ThemeType.Light);
            }
        }
    }

    public SettingsViewModel(DataExportService dataExportService, DataImportService dataImportService)
    {
        _dataExportService = dataExportService;
        _dataImportService = dataImportService;
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
        WeakReferenceMessenger.Default.Send(new CalendarSettingsChangedMessage(newSystem));
    }

    private void OnThemeSelectionChanged(ThemeType selectedTheme)
    {
        if (AppSettings.SelectedTheme != selectedTheme)
        {
            // Update all theme flags based on the selected one
            SetProperty(ref _isPitchBlackSelected, selectedTheme == ThemeType.PitchBlack, nameof(IsPitchBlackSelected));
            SetProperty(ref _isDarkGraySelected, selectedTheme == ThemeType.DarkGray, nameof(IsDarkGraySelected));
            SetProperty(ref _isNordSelected, selectedTheme == ThemeType.Nord, nameof(IsNordSelected));
            SetProperty(ref _isCatppuccinMochaSelected, selectedTheme == ThemeType.CatppuccinMocha, nameof(IsCatppuccinMochaSelected)); // Updated
            SetProperty(ref _isLightSelected, selectedTheme == ThemeType.Light, nameof(IsLightSelected));
            UpdateThemeSetting(selectedTheme);
        }
    }

    private void UpdateThemeSetting(ThemeType newTheme)
    {
        if (AppSettings.SelectedTheme == newTheme)
        {
            return;
        }

        AppSettings.SelectedTheme = newTheme;
        Preferences.Set(AppSettings.ThemePreferenceKey, (int)newTheme);
        WeakReferenceMessenger.Default.Send(new ThemeChangedMessage(newTheme));
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

        ThemeType currentTheme = AppSettings.SelectedTheme;
        bool shouldBePitchBlack = currentTheme == ThemeType.PitchBlack;
        bool shouldBeDarkGray = currentTheme == ThemeType.DarkGray;
        bool shouldBeNord = currentTheme == ThemeType.Nord;
        bool shouldBeCatppuccin = currentTheme == ThemeType.CatppuccinMocha; // Added check
        bool shouldBeLight = currentTheme == ThemeType.Light;
        SetProperty(ref _isPitchBlackSelected, shouldBePitchBlack, nameof(IsPitchBlackSelected));
        SetProperty(ref _isDarkGraySelected, shouldBeDarkGray, nameof(IsDarkGraySelected));
        SetProperty(ref _isNordSelected, shouldBeNord, nameof(IsNordSelected));
        SetProperty(ref _isCatppuccinMochaSelected, shouldBeCatppuccin, nameof(IsCatppuccinMochaSelected)); // Set property
        SetProperty(ref _isLightSelected, shouldBeLight, nameof(IsLightSelected));

        // Check if any theme is selected after loading preferences
        bool anyThemeSelected = _isPitchBlackSelected || _isDarkGraySelected || _isNordSelected || _isCatppuccinMochaSelected || _isLightSelected;

        if (!anyThemeSelected) // Apply default only if nothing is selected (covers initial run and potential invalid pref value)
        {
            AppTheme systemTheme = Application.Current?.RequestedTheme ?? AppTheme.Dark;
            ThemeType defaultTheme = systemTheme == AppTheme.Light ? ThemeType.Light : ThemeType.PitchBlack; // Or Catppuccin? Let's stick to PitchBlack default dark.
            OnThemeSelectionChanged(defaultTheme);
            Debug.WriteLine($"LoadSettings: No valid theme selected. Defaulting based on system theme to: {defaultTheme}");
        }
    }

    [RelayCommand]
    private async Task ExportDataAsync()
    {
        Debug.WriteLine("SettingsViewModel.ExportDataAsync: Starting data export process.");
        try
        {
            CleanUpOldExportFiles(OldTaskExportFilePrefix, ".json");
            CleanUpOldExportFiles(NewDataExportFilePrefix, ".json");

            bool success = await _dataExportService.ExportDataAsync();

            if (success)
            {
                Debug.WriteLine("SettingsViewModel.ExportDataAsync: ExportDataAsync from service reported success.");
            }
            else
            {
                Debug.WriteLine("SettingsViewModel.ExportDataAsync: ExportDataAsync from service reported failure or cancellation.");
                await ShowAlertAsync("Export Failed", "Could not export Tickly data. The operation may have been cancelled or an error occurred.", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SettingsViewModel.ExportDataAsync: Exception caught: {ex.GetType().Name} - {ex.Message}\nStackTrace: {ex.StackTrace}");
            await ShowAlertAsync("Export Error", $"An error occurred during data export: {ex.Message}", "OK");
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
                catch (Exception ex)
                {
                    Debug.WriteLine($"CleanUpOldExportFiles: Error deleting old file '{file}': {ex.Message}");
                }
            }
            Debug.WriteLine($"CleanUpOldExportFiles: Deleted {count} old export files matching pattern '{searchPattern}'.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CleanUpOldExportFiles: Error during cleanup for pattern '{prefix}*{extension}': {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ImportDataAsync()
    {
        Debug.WriteLine("SettingsViewModel.ImportDataAsync: Starting data import process.");
        try
        {
            bool confirmed = await ShowConfirmationAsync(
                "Confirm Import",
                "This will REPLACE your current tasks, settings, and progress with the content of the selected file. This cannot be undone. Proceed?",
                "Replace All Data",
                "Cancel");

            if (!confirmed)
            {
                Debug.WriteLine("SettingsViewModel.ImportDataAsync: User cancelled confirmation before file picking.");
                return;
            }

            bool success = await _dataImportService.ImportDataAsync();

            if (success)
            {
                Debug.WriteLine("SettingsViewModel.ImportDataAsync: ImportDataAsync from service reported success.");
                await ShowAlertAsync("Import Successful", "Tickly data (tasks, settings, progress) imported successfully. The application will reflect the changes.", "OK");
                LoadSettings();
            }
            else
            {
                Debug.WriteLine("SettingsViewModel.ImportDataAsync: ImportDataAsync from service reported failure or cancellation.");
                await ShowAlertAsync("Import Failed", "Could not import Tickly data. The file might be invalid, the operation cancelled, or an error occurred.", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SettingsViewModel.ImportDataAsync: Exception caught: {ex.GetType().Name} - {ex.Message}\nStackTrace: {ex.StackTrace}");
            await ShowAlertAsync("Import Error", $"An unexpected error occurred during data import: {ex.Message}", "OK");
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
        Page? currentPage = Application.Current?.MainPage;

        if (currentPage is Shell shell)
        {
            currentPage = shell.CurrentPage;
        }
        else if (currentPage is NavigationPage navPage)
        {
            currentPage = navPage.CurrentPage;
        }
        else if (currentPage is TabbedPage tabbedPage)
        {
            currentPage = tabbedPage.CurrentPage;
        }

        if (currentPage == null && Application.Current?.Windows is { Count: > 0 } windows && windows[0].Page is not null)
        {
            currentPage = windows[0].Page;
            if (currentPage is NavigationPage navPageModal && navPageModal.CurrentPage != null)
            {
                currentPage = navPageModal.CurrentPage;
            }
        }

        if (currentPage == null)
        {
            Debug.WriteLine("GetCurrentPage: Could not determine the current page reliably.");
        }
        return currentPage;
    }
}