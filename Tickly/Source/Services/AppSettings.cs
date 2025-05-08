using System.Diagnostics;
using Tickly.Models;

namespace Tickly.Services;

public static class AppSettings
{
    public const string CalendarSystemKey = "CalendarSystemPreference";
    public const string ThemePreferenceKey = "ThemePreference";
    public const string UseWindowsSystemBackgroundKey = "UseWindowsSystemBackground"; // Added Key

    private static CalendarSystemType _selectedCalendarSystem;
    private static ThemeType _selectedTheme;
    private static bool _useWindowsSystemBackground; // Added Field

    static AppSettings()
    {
        int storedCalendarValue = Preferences.Get(CalendarSystemKey, (int)CalendarSystemType.Gregorian);
        _selectedCalendarSystem = (CalendarSystemType)storedCalendarValue;
        Debug.WriteLine($"AppSettings (Static Constructor): Initialized CalendarSystem to {_selectedCalendarSystem} from Preferences.");

        int storedThemeValue = Preferences.Get(ThemePreferenceKey, (int)ThemeType.PitchBlack); // Default to PitchBlack if not set
        _selectedTheme = (ThemeType)storedThemeValue;
        Debug.WriteLine($"AppSettings (Static Constructor): Initialized Theme to {_selectedTheme} from Preferences.");

        // Load the system background setting, default to false
        _useWindowsSystemBackground = Preferences.Get(UseWindowsSystemBackgroundKey, false);
        Debug.WriteLine($"AppSettings (Static Constructor): Initialized UseWindowsSystemBackground to {_useWindowsSystemBackground} from Preferences.");
    }

    public static CalendarSystemType SelectedCalendarSystem
    {
        get => _selectedCalendarSystem;
        set
        {
            if (_selectedCalendarSystem != value)
            {
                _selectedCalendarSystem = value;
                Debug.WriteLine($"AppSettings: CalendarSystem changed to {value}.");
                // Note: Saving is handled by SettingsViewModel to ensure UI updates
            }
        }
    }

    public static ThemeType SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (_selectedTheme != value)
            {
                _selectedTheme = value;
                Debug.WriteLine($"AppSettings: Theme changed to {value}.");
                // Note: Saving is handled by SettingsViewModel to ensure UI updates
            }
        }
    }

    // Added Property for system background
    public static bool UseWindowsSystemBackground
    {
        get => _useWindowsSystemBackground;
        set
        {
            if (_useWindowsSystemBackground != value)
            {
                _useWindowsSystemBackground = value;
                Debug.WriteLine($"AppSettings: UseWindowsSystemBackground changed to {value}.");
                // Note: Saving is handled by SettingsViewModel to ensure UI updates
            }
        }
    }
}