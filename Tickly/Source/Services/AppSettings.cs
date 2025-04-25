using System.Diagnostics;
using Tickly.Models;

namespace Tickly.Services;

public static class AppSettings
{
    // Key for storing the setting
    public const string CalendarSystemKey = "CalendarSystemPreference";
    public const string SortOrderKey = "SortOrderPreference"; // Key for sort order

    // Backing field - Now initialized by static constructor
    private static CalendarSystemType _selectedCalendarSystem;
    private static SortOrderType _selectedSortOrder; // Backing field for sort order

    // --- Static Constructor ---
    // This runs automatically the first time the AppSettings class is accessed.
    static AppSettings()
    {
        // Load Calendar from Preferences, default to Gregorian (0) if not found
        int storedCalendarValue = Preferences.Get(CalendarSystemKey, (int)CalendarSystemType.Gregorian);
        _selectedCalendarSystem = (CalendarSystemType)storedCalendarValue; // Set the backing field directly
        Debug.WriteLine($"AppSettings (Static Constructor): Initialized CalendarSystem to {_selectedCalendarSystem} from Preferences.");

        // Load Sort Order from Preferences, default to PriorityHighFirst (1) if not found
        int storedSortValue = Preferences.Get(SortOrderKey, (int)SortOrderType.PriorityHighFirst);
        _selectedSortOrder = (SortOrderType)storedSortValue; // Set the backing field directly
        Debug.WriteLine($"AppSettings (Static Constructor): Initialized SortOrder to {_selectedSortOrder} from Preferences.");
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
                _selectedCalendarSystem = value; // Update backing field
                // Preferences are saved by SettingsViewModel when user makes changes
                Debug.WriteLine($"AppSettings: CalendarSystem changed to {value} (will be saved by SettingsViewModel).");
                // Optionally raise an event if needed for non-UI immediate updates
                // SettingsChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    // Public property to access the Sort Order setting
    public static SortOrderType SelectedSortOrder
    {
        get => _selectedSortOrder;
        set
        {
            if (_selectedSortOrder != value)
            {
                _selectedSortOrder = value; // Update backing field
                Preferences.Set(SortOrderKey, (int)value); // Save immediately when set
                Debug.WriteLine($"AppSettings: SortOrder changed and saved to {value}.");
                // Optionally raise an event if needed for non-UI immediate updates
                // SortOrderChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    // Optional: Event for real-time updates elsewhere if needed
    // public static event EventHandler SettingsChanged;
    // public static event EventHandler SortOrderChanged;
}