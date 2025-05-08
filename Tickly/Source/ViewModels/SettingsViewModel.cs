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

    // Calendar Properties
    private bool _isGregorianSelected;
    private bool _isPersianSelected;

    // Theme Properties
    private bool _isPitchBlackSelected;
    private bool _isDarkGraySelected;
    private bool _isNordSelected;
    private bool _isCatppuccinMochaSelected;
    private bool _isSolarizedDarkSelected;
    private bool _isGruvboxDarkSelected;
    private bool _isMonokaiSelected;
    private bool _isLightSelected;
    private bool _isSolarizedLightSelected;
    private bool _isSepiaSelected;
    private bool _isHighContrastDarkSelected;
    private bool _isHighContrastLightSelected;

    private const string OldTaskExportFilePrefix = "Tickly-Tasks-Export-";
    private const string NewDataExportFilePrefix = "Tickly_";

    #region Calendar Properties Getters/Setters
    public bool IsGregorianSelected
    {
        get => _isGregorianSelected;
        set { if (SetProperty(ref _isGregorianSelected, value) && value) OnCalendarSelectionChanged(true); }
    }
    public bool IsPersianSelected
    {
        get => _isPersianSelected;
        set { if (SetProperty(ref _isPersianSelected, value) && value) OnCalendarSelectionChanged(false); }
    }
    #endregion

    #region Theme Properties Getters/Setters
    public bool IsPitchBlackSelected
    {
        get => _isPitchBlackSelected;
        set { if (SetProperty(ref _isPitchBlackSelected, value) && value) OnThemeSelectionChanged(ThemeType.PitchBlack); }
    }
    public bool IsDarkGraySelected
    {
        get => _isDarkGraySelected;
        set { if (SetProperty(ref _isDarkGraySelected, value) && value) OnThemeSelectionChanged(ThemeType.DarkGray); }
    }
    public bool IsNordSelected
    {
        get => _isNordSelected;
        set { if (SetProperty(ref _isNordSelected, value) && value) OnThemeSelectionChanged(ThemeType.Nord); }
    }
    public bool IsCatppuccinMochaSelected
    {
        get => _isCatppuccinMochaSelected;
        set { if (SetProperty(ref _isCatppuccinMochaSelected, value) && value) OnThemeSelectionChanged(ThemeType.CatppuccinMocha); }
    }
    public bool IsSolarizedDarkSelected
    {
        get => _isSolarizedDarkSelected;
        set { if (SetProperty(ref _isSolarizedDarkSelected, value) && value) OnThemeSelectionChanged(ThemeType.SolarizedDark); }
    }
    public bool IsGruvboxDarkSelected
    {
        get => _isGruvboxDarkSelected;
        set { if (SetProperty(ref _isGruvboxDarkSelected, value) && value) OnThemeSelectionChanged(ThemeType.GruvboxDark); }
    }
    public bool IsMonokaiSelected
    {
        get => _isMonokaiSelected;
        set { if (SetProperty(ref _isMonokaiSelected, value) && value) OnThemeSelectionChanged(ThemeType.Monokai); }
    }
    public bool IsLightSelected
    {
        get => _isLightSelected;
        set { if (SetProperty(ref _isLightSelected, value) && value) OnThemeSelectionChanged(ThemeType.Light); }
    }
    public bool IsSolarizedLightSelected
    {
        get => _isSolarizedLightSelected;
        set { if (SetProperty(ref _isSolarizedLightSelected, value) && value) OnThemeSelectionChanged(ThemeType.SolarizedLight); }
    }
    public bool IsSepiaSelected
    {
        get => _isSepiaSelected;
        set { if (SetProperty(ref _isSepiaSelected, value) && value) OnThemeSelectionChanged(ThemeType.Sepia); }
    }
    public bool IsHighContrastDarkSelected
    {
        get => _isHighContrastDarkSelected;
        set { if (SetProperty(ref _isHighContrastDarkSelected, value) && value) OnThemeSelectionChanged(ThemeType.HighContrastDark); }
    }
    public bool IsHighContrastLightSelected
    {
        get => _isHighContrastLightSelected;
        set { if (SetProperty(ref _isHighContrastLightSelected, value) && value) OnThemeSelectionChanged(ThemeType.HighContrastLight); }
    }
    #endregion

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
        if (AppSettings.SelectedCalendarSystem == newSystem) return;
        AppSettings.SelectedCalendarSystem = newSystem;
        Preferences.Set(AppSettings.CalendarSystemKey, (int)newSystem);
        WeakReferenceMessenger.Default.Send(new CalendarSettingsChangedMessage(newSystem));
    }

    private void OnThemeSelectionChanged(ThemeType selectedTheme)
    {
        if (AppSettings.SelectedTheme == selectedTheme) return; // Avoid unnecessary updates

        // Update all theme flags - only the selected one will be true
        SetProperty(ref _isPitchBlackSelected, selectedTheme == ThemeType.PitchBlack, nameof(IsPitchBlackSelected));
        SetProperty(ref _isDarkGraySelected, selectedTheme == ThemeType.DarkGray, nameof(IsDarkGraySelected));
        SetProperty(ref _isNordSelected, selectedTheme == ThemeType.Nord, nameof(IsNordSelected));
        SetProperty(ref _isCatppuccinMochaSelected, selectedTheme == ThemeType.CatppuccinMocha, nameof(IsCatppuccinMochaSelected));
        SetProperty(ref _isSolarizedDarkSelected, selectedTheme == ThemeType.SolarizedDark, nameof(IsSolarizedDarkSelected));
        SetProperty(ref _isGruvboxDarkSelected, selectedTheme == ThemeType.GruvboxDark, nameof(IsGruvboxDarkSelected));
        SetProperty(ref _isMonokaiSelected, selectedTheme == ThemeType.Monokai, nameof(IsMonokaiSelected));
        SetProperty(ref _isLightSelected, selectedTheme == ThemeType.Light, nameof(IsLightSelected));
        SetProperty(ref _isSolarizedLightSelected, selectedTheme == ThemeType.SolarizedLight, nameof(IsSolarizedLightSelected));
        SetProperty(ref _isSepiaSelected, selectedTheme == ThemeType.Sepia, nameof(IsSepiaSelected));
        SetProperty(ref _isHighContrastDarkSelected, selectedTheme == ThemeType.HighContrastDark, nameof(IsHighContrastDarkSelected));
        SetProperty(ref _isHighContrastLightSelected, selectedTheme == ThemeType.HighContrastLight, nameof(IsHighContrastLightSelected));

        UpdateThemeSetting(selectedTheme);
    }


    private void UpdateThemeSetting(ThemeType newTheme)
    {
        // This check is now redundant due to the check in OnThemeSelectionChanged, but harmless
        if (AppSettings.SelectedTheme == newTheme) return;

        AppSettings.SelectedTheme = newTheme;
        Preferences.Set(AppSettings.ThemePreferenceKey, (int)newTheme);
        WeakReferenceMessenger.Default.Send(new ThemeChangedMessage(newTheme));
    }

    private void LoadSettings()
    {
        // Load Calendar Setting
        CalendarSystemType currentSystem = AppSettings.SelectedCalendarSystem;
        _isGregorianSelected = currentSystem == CalendarSystemType.Gregorian;
        _isPersianSelected = currentSystem == CalendarSystemType.Persian;
        OnPropertyChanged(nameof(IsGregorianSelected));
        OnPropertyChanged(nameof(IsPersianSelected));
        if (!_isGregorianSelected && !_isPersianSelected)
        {
            // Default preference logic if needed
            _isGregorianSelected = true; OnPropertyChanged(nameof(IsGregorianSelected));
            AppSettings.SelectedCalendarSystem = CalendarSystemType.Gregorian;
            Preferences.Set(AppSettings.CalendarSystemKey, (int)CalendarSystemType.Gregorian);
        }

        // Load Theme Setting
        ThemeType currentTheme = AppSettings.SelectedTheme;
        _isPitchBlackSelected = currentTheme == ThemeType.PitchBlack;
        _isDarkGraySelected = currentTheme == ThemeType.DarkGray;
        _isNordSelected = currentTheme == ThemeType.Nord;
        _isCatppuccinMochaSelected = currentTheme == ThemeType.CatppuccinMocha;
        _isSolarizedDarkSelected = currentTheme == ThemeType.SolarizedDark;
        _isGruvboxDarkSelected = currentTheme == ThemeType.GruvboxDark;
        _isMonokaiSelected = currentTheme == ThemeType.Monokai;
        _isLightSelected = currentTheme == ThemeType.Light;
        _isSolarizedLightSelected = currentTheme == ThemeType.SolarizedLight;
        _isSepiaSelected = currentTheme == ThemeType.Sepia;
        _isHighContrastDarkSelected = currentTheme == ThemeType.HighContrastDark;
        _isHighContrastLightSelected = currentTheme == ThemeType.HighContrastLight;

        // Notify changes for all theme properties
        OnPropertyChanged(nameof(IsPitchBlackSelected));
        OnPropertyChanged(nameof(IsDarkGraySelected));
        OnPropertyChanged(nameof(IsNordSelected));
        OnPropertyChanged(nameof(IsCatppuccinMochaSelected));
        OnPropertyChanged(nameof(IsSolarizedDarkSelected));
        OnPropertyChanged(nameof(IsGruvboxDarkSelected));
        OnPropertyChanged(nameof(IsMonokaiSelected));
        OnPropertyChanged(nameof(IsLightSelected));
        OnPropertyChanged(nameof(IsSolarizedLightSelected));
        OnPropertyChanged(nameof(IsSepiaSelected));
        OnPropertyChanged(nameof(IsHighContrastDarkSelected));
        OnPropertyChanged(nameof(IsHighContrastLightSelected));

        // Check if *any* theme property is true after loading
        bool anyThemeSelected = _isPitchBlackSelected || _isDarkGraySelected || _isNordSelected ||
                                _isCatppuccinMochaSelected || _isSolarizedDarkSelected || _isGruvboxDarkSelected ||
                                _isMonokaiSelected || _isLightSelected || _isSolarizedLightSelected ||
                                _isSepiaSelected || _isHighContrastDarkSelected || _isHighContrastLightSelected;


        if (!anyThemeSelected)
        {
            // If no theme is selected (e.g., preference was invalid or first run)
            AppTheme systemTheme = Application.Current?.RequestedTheme ?? AppTheme.Dark;
            ThemeType defaultTheme = systemTheme == AppTheme.Light ? ThemeType.Light : ThemeType.PitchBlack;
            OnThemeSelectionChanged(defaultTheme); // This will update the correct bool property and settings
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

            if (!success)
            {
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
            if (!Directory.Exists(cacheDir)) return;

            string searchPattern = $"{prefix}*{extension}";
            IEnumerable<string> oldFiles = Directory.EnumerateFiles(cacheDir, searchPattern);
            int count = 0;
            foreach (string file in oldFiles)
            {
                try
                {
                    File.Delete(file);
                    count++;
                }
                catch (Exception ex) { Debug.WriteLine($"CleanUpOldExportFiles: Error deleting old file '{file}': {ex.Message}"); }
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

            if (!confirmed) return;

            bool success = await _dataImportService.ImportDataAsync();

            if (success)
            {
                await ShowAlertAsync("Import Successful", "Tickly data imported successfully. The application will reflect the changes.", "OK");
                LoadSettings(); // Reload VM settings to match imported data
            }
            else
            {
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
        if (currentPage is not null) await currentPage.DisplayAlert(title, message, cancelAction);
        else Debug.WriteLine($"ShowAlert: Could not find current page to display alert: {title}");
    }

    private static async Task<bool> ShowConfirmationAsync(string title, string message, string acceptAction, string cancelAction)
    {
        if (!MainThread.IsMainThread)
        {
            return await MainThread.InvokeOnMainThreadAsync(() => ShowConfirmationAsync(title, message, acceptAction, cancelAction));
        }
        Page? currentPage = GetCurrentPage();
        if (currentPage is not null) return await currentPage.DisplayAlert(title, message, acceptAction, cancelAction);
        else { Debug.WriteLine($"ShowConfirmation: Could not find current page to display confirmation: {title}"); return false; }
    }

    private static Page? GetCurrentPage()
    {
        Page? currentPage = Application.Current?.MainPage;
        if (currentPage is Shell shell) currentPage = shell.CurrentPage;
        else if (currentPage is NavigationPage navPage) currentPage = navPage.CurrentPage;
        else if (currentPage is TabbedPage tabbedPage) currentPage = tabbedPage.CurrentPage;

        if (currentPage == null && Application.Current?.Windows is { Count: > 0 } windows && windows[0].Page is not null)
        {
            currentPage = windows[0].Page;
            if (currentPage is NavigationPage navPageModal && navPageModal.CurrentPage != null) currentPage = navPageModal.CurrentPage;
        }
        if (currentPage == null) Debug.WriteLine("GetCurrentPage: Could not determine the current page reliably.");
        return currentPage;
    }
}