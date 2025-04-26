using Tickly.Models;

namespace Tickly.Services;

public static class AppSettings
{
    public const string CalendarSystemKey = "CalendarSystemPreference";
    public const string SortOrderKey = "SortOrderPreference";

    public static CalendarSystemType SelectedCalendarSystem
    {
        get;

        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
        }
    }

    public static SortOrderType SelectedSortOrder
    {
        get;

        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Preferences.Set(SortOrderKey, (int)value);
        }
    }

    static AppSettings()
    {
        int storedCalendarValue = Preferences.Get(CalendarSystemKey, (int)CalendarSystemType.Gregorian);
        SelectedCalendarSystem = (CalendarSystemType)storedCalendarValue;

        int storedSortValue = Preferences.Get(SortOrderKey, (int)SortOrderType.PriorityHighFirst);
        SelectedSortOrder = (SortOrderType)storedSortValue;
    }
}