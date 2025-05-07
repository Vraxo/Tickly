using System.Diagnostics;
using Tickly.Models;

namespace Tickly.Services;

public static class AppSettings
{
    // Key for storing the setting
    public const string CalendarSystemKey = "CalendarSystemPreference";
    // REMOVED: SortOrderKey
    // public const string SortOrderKey = "SortOrderPreference";

    // Backing field - Now initialized by static constructor
    private static CalendarSystemType _selectedCalendarSystem;
    // REMOVED: _selectedSortOrder
    // private static SortOrderType _selectedSortOrder;

    // --- Static Constructor ---
    static AppSettings()
    {
        // Load Calendar from Preferences, default to Gregorian (0) if not found
        int storedCalendarValue = Preferences.Get(CalendarSystemKey, (int)CalendarSystemType.Gregorian);
        _selectedCalendarSystem = (CalendarSystemType)storedCalendarValue;
        Debug.WriteLine($"AppSettings (Static Constructor): Initialized CalendarSystem to {_selectedCalendarSystem} from Preferences.");

        // REMOVED: Sort Order loading
        // int storedSortValue = Preferences.Get(SortOrderKey, (int)SortOrderType.PriorityHighFirst);
        // _selectedSortOrder = (SortOrderType)storedSortValue;
        // Debug.WriteLine($"AppSettings (Static Constructor): Initialized SortOrder to {_selectedSortOrder} from Preferences.");
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
                Debug.WriteLine($"AppSettings: CalendarSystem changed to {value} (will be saved by SettingsViewModel).");
            }
        }
    }

    // REMOVED: SelectedSortOrder property
    // public static SortOrderType SelectedSortOrder { ... }

}