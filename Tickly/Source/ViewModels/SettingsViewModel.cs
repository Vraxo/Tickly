// ViewModels/SettingsViewModel.cs (New File)
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Storage; // For Preferences
using System.ComponentModel;
using Tickly.Models;
using Tickly.Services; // For AppSettings

namespace Tickly.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isGregorianSelected;

    [ObservableProperty]
    private bool _isPersianSelected;

    public SettingsViewModel()
    {
        LoadSettings();
        // Subscribe to changes in the boolean properties to save settings
        this.PropertyChanged += SettingsViewModel_PropertyChanged;
    }

    private void LoadSettings()
    {
        // Load from Preferences, default to Gregorian (0) if not found
        int storedValue = Preferences.Get(AppSettings.CalendarSystemKey, (int)CalendarSystemType.Gregorian);
        var loadedSystem = (CalendarSystemType)storedValue;

        System.Diagnostics.Debug.WriteLine($"SettingsViewModel: Loading setting - StoredValue={storedValue}, LoadedSystem={loadedSystem}");

        // Update the static AppSettings
        AppSettings.SelectedCalendarSystem = loadedSystem;

        // Update the boolean properties for RadioButton binding
        IsGregorianSelected = loadedSystem == CalendarSystemType.Gregorian;
        IsPersianSelected = loadedSystem == CalendarSystemType.Persian;
    }

    private void SettingsViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        CalendarSystemType newSystem = CalendarSystemType.Gregorian; // Default assumption

        // Determine the new system based on which boolean property changed *to true*
        if (e.PropertyName == nameof(IsGregorianSelected) && IsGregorianSelected)
        {
            newSystem = CalendarSystemType.Gregorian;
            // Ensure the other option is deselected (though RadioButton group should handle this)
            if (IsPersianSelected) IsPersianSelected = false;
        }
        else if (e.PropertyName == nameof(IsPersianSelected) && IsPersianSelected)
        {
            newSystem = CalendarSystemType.Persian;
            // Ensure the other option is deselected
            if (IsGregorianSelected) IsGregorianSelected = false;
        }
        else
        {
            // If the property changed to false, or another property changed, do nothing here
            return;
        }

        // Check if the system actually changed from the currently loaded setting
        if (AppSettings.SelectedCalendarSystem != newSystem)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsViewModel: PropertyChanged detected change to {newSystem}");

            // Save to Preferences
            Preferences.Set(AppSettings.CalendarSystemKey, (int)newSystem);

            // Update the static AppSettings immediately
            AppSettings.SelectedCalendarSystem = newSystem;

            // Optional: Trigger UI refresh elsewhere if needed without restart
            // MainViewModel might need a Refresh command or listen to AppSettings.SettingsChanged
        }
    }
}