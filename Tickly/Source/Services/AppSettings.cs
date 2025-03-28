// Services/AppSettings.cs
using Microsoft.Maui.Storage; // Required for Preferences
using System.Diagnostics;
using Tickly.Models;

namespace Tickly.Services
{
    public static class AppSettings
    {
        // Key for storing the setting
        public const string CalendarSystemKey = "CalendarSystemPreference";

        // Backing field - Now initialized by static constructor
        private static CalendarSystemType _selectedCalendarSystem;

        // --- Static Constructor ---
        // This runs automatically the first time the AppSettings class is accessed.
        static AppSettings()
        {
            // Load from Preferences, default to Gregorian (0) if not found
            int storedValue = Preferences.Get(CalendarSystemKey, (int)CalendarSystemType.Gregorian);
            _selectedCalendarSystem = (CalendarSystemType)storedValue; // Set the backing field directly
            Debug.WriteLine($"AppSettings (Static Constructor): Initialized CalendarSystem to {_selectedCalendarSystem} from Preferences.");
        }
        // --- End Static Constructor ---

        // Public property to access the setting
        public static CalendarSystemType SelectedCalendarSystem
        {
            get => _selectedCalendarSystem;
            set
            {
                if (_selectedCalendarSystem != value)
                {
                    _selectedCalendarSystem = value; // Update backing field
                    // Preferences are saved by SettingsViewModel when user makes changes
                    Debug.WriteLine($"AppSettings: CalendarSystem changed to {value} (will be saved by SettingsViewModel).");
                    // Optionally raise an event if needed for non-UI immediate updates
                    // SettingsChanged?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        // Optional: Event for real-time updates elsewhere if needed
        // public static event EventHandler SettingsChanged;
    }
}