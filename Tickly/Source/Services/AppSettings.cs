// Services/AppSettings.cs (New File)
using Tickly.Models;

namespace Tickly.Services;

public static class AppSettings
{
    // Default to Gregorian
    private static CalendarSystemType _selectedCalendarSystem = CalendarSystemType.Gregorian;
    public static CalendarSystemType SelectedCalendarSystem
    {
        get => _selectedCalendarSystem;
        set
        {
            if (_selectedCalendarSystem != value)
            {
                _selectedCalendarSystem = value;
                // Optionally raise an event if other parts of the app
                // need to react immediately without restarting.
                // SettingsChanged?.Invoke(null, EventArgs.Empty);
                System.Diagnostics.Debug.WriteLine($"AppSettings: CalendarSystem changed to {value}");
            }
        }
    }

    // Optional: Event for real-time updates elsewhere if needed
    // public static event EventHandler SettingsChanged;

    // Key for storing the setting
    public const string CalendarSystemKey = "CalendarSystemPreference";
}