using System.Diagnostics;

using System.Diagnostics;
using Tickly.Models;

namespace Tickly.Services;

public static class AppSettings
{
    // Keys for storing the settings
    public const string CalendarSystemKey = "CalendarSystemPreference";
    public const string DarkModeBackgroundKey = "DarkModeBackgroundPreference"; // New Key

    // Backing fields - Now initialized by static constructor
    private static CalendarSystemType _selectedCalendarSystem;
    private static DarkModeBackgroundType _selectedDarkModeBackground; // New Field

    // --- Static Constructor ---
    static AppSettings()
    {
        // Load Calendar from Preferences, default to Gregorian (0) if not found
        int storedCalendarValue = Preferences.Get(CalendarSystemKey, (int)CalendarSystemType.Gregorian);
        _selectedCalendarSystem = (CalendarSystemType)storedCalendarValue;
        Debug.WriteLine($"AppSettings (Static Constructor): Initialized CalendarSystem to {_selectedCalendarSystem} from Preferences.");

        // Load Dark Mode Background from Preferences, default to OffBlack (0) if not found
        int storedDarkModeBgValue = Preferences.Get(DarkModeBackgroundKey, (int)DarkModeBackgroundType.OffBlack);
        _selectedDarkModeBackground = (DarkModeBackgroundType)storedDarkModeBgValue;
        Debug.WriteLine($"AppSettings (Static Constructor): Initialized DarkModeBackground to {_selectedDarkModeBackground} from Preferences.");

    }
    // --- End Static Constructor ---

    // Public property to access the Calendar setting
    public static CalendarSystemType SelectedCalendarSystem
    {
        get => _selectedCalendarSystem;
        set
        {
            if (_selectedCalendarSystem != value)
            {
                _selectedCalendarSystem = value;
                // Saving is handled by ViewModel
                Debug.WriteLine($"AppSettings: CalendarSystem changed to {value} (will be saved by SettingsViewModel).");
            }
        }
    }

    // Public property to access the Dark Mode Background setting
    public static DarkModeBackgroundType SelectedDarkModeBackground
    {
        get => _selectedDarkModeBackground;
        set
        {
            if (_selectedDarkModeBackground != value)
            {
                _selectedDarkModeBackground = value;
                // Saving is handled by ViewModel
                Debug.WriteLine($"AppSettings: DarkModeBackground changed to {value} (will be saved by SettingsViewModel).");
            }
        }
    }
}