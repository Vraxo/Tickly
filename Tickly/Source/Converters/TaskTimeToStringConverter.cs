using System.Diagnostics;
using System.Globalization;
using Tickly.Models;
using Tickly.Services;

namespace Tickly.Converters;

public class TaskTimeToStringConverter : IValueConverter
{
    private static readonly string[] PersianAbbreviatedMonthNames =
    [
        "", "فرور", "اردی", "خرد", "تیر", "مرد", "شهر", "مهر", "آبا", "آذر", "دی", "بهم", "اسف"
    ];

    private const string NoDateString = "No date";
    private const string AnyTimeString = "Any time";
    private const string DateErrorString = "Date Error";
    private const string TodayString = "Today";
    private const string TomorrowString = "Tomorrow";
    private const string UnknownDueDateString = "Unknown";
    private const string DefaultGregorianDateFormat = "ddd, dd MMM yyyy";
    private const string ShortGregorianDateFormat = "dd MMM";
    private const string DayAndShortGregorianDateFormat = "ddd, dd MMM";
    private const string FallbackIsoDateFormat = "yyyy-MM-dd";
    private const string RepeatingPrefix = "Repeating";
    private const string DailyRepetition = "Daily";
    private const string AlternateDayRepetition = "Every other day";
    private const string WeeklyRepetitionFormat = "Weekly on {0}";
    private const string DuePrefix = "due";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TaskItem task)
        {
            return string.Empty;
        }

        CalendarSystemType calendarSystem = AppSettings.SelectedCalendarSystem;
        CultureInfo formatCulture = GetFormatCulture(calendarSystem);

        try
        {
            return FormatTaskTime(task, calendarSystem, formatCulture);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in TaskTimeToStringConverter for task '{task.Title}': {ex.Message}");
            return DateErrorString;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static CultureInfo GetFormatCulture(CalendarSystemType calendarSystem)
    {
        return calendarSystem == CalendarSystemType.Persian
            ? new("fa-IR")
            : CultureInfo.InvariantCulture;
    }

    private static string FormatTaskTime(TaskItem task, CalendarSystemType calendarSystem, CultureInfo formatCulture)
    {
        return task.TimeType switch
        {
            TaskTimeType.SpecificDate => FormatSpecificDateTask(task, calendarSystem, formatCulture),
            TaskTimeType.Repeating => FormatRepeatingTask(task, calendarSystem, formatCulture),
            TaskTimeType.None => AnyTimeString,
            _ => AnyTimeString,
        };
    }

    private static string FormatSpecificDateTask(TaskItem task, CalendarSystemType calendarSystem, CultureInfo formatCulture)
    {
        return task.DueDate is null
            ? NoDateString
            : FormatDate(task.DueDate.Value, calendarSystem, formatCulture, DefaultGregorianDateFormat);
    }

    private static string FormatRepeatingTask(TaskItem task, CalendarSystemType calendarSystem, CultureInfo formatCulture)
    {
        string repetition = GetRepetitionString(task, formatCulture);
        string nextDueDateString = task.DueDate.HasValue
            ? FormatDate(task.DueDate.Value, calendarSystem, formatCulture, DayAndShortGregorianDateFormat)
            : UnknownDueDateString;

        return $"{repetition}, {DuePrefix} {nextDueDateString}";
    }

    private static string GetRepetitionString(TaskItem task, CultureInfo formatCulture)
    {
        return task.RepetitionType switch
        {
            TaskRepetitionType.Daily => DailyRepetition,
            TaskRepetitionType.AlternateDay => AlternateDayRepetition,
            TaskRepetitionType.Weekly => string.Format(formatCulture, WeeklyRepetitionFormat, GetDayName(task.RepetitionDayOfWeek, formatCulture)),
            _ => RepeatingPrefix
        };
    }

    private static string FormatDate(DateTime date, CalendarSystemType system, CultureInfo formatCulture, string gregorianFormat)
    {
        string? specialDay = GetSpecialDayString(date.Date);
        if (specialDay != null)
        {
            if (gregorianFormat == DefaultGregorianDateFormat || gregorianFormat == DayAndShortGregorianDateFormat || gregorianFormat == ShortGregorianDateFormat)
            {
                return specialDay;
            }
        }

        return system == CalendarSystemType.Persian
            ? FormatPersianDate(date, formatCulture, gregorianFormat)
            : FormatGregorianDate(date, formatCulture, gregorianFormat);
    }

    private static string? GetSpecialDayString(DateTime date)
    {
        DateTime today = DateTime.Today;
        DateTime tomorrow = today.AddDays(1);

        if (date == today) return TodayString;
        if (date == tomorrow) return TomorrowString;
        return null;
    }

    private static string FormatPersianDate(DateTime date, CultureInfo formatCulture, string gregorianFormat)
    {
        try
        {
            PersianCalendar pc = new();
            int year = pc.GetYear(date);
            int month = pc.GetMonth(date);
            int day = pc.GetDayOfMonth(date);
            string dayName = formatCulture.DateTimeFormat.GetAbbreviatedDayName(pc.GetDayOfWeek(date));

            string monthName = month >= 1 && month <= 12
                ? PersianAbbreviatedMonthNames[month]
                : "?";

            return gregorianFormat switch
            {
                DefaultGregorianDateFormat => $"{dayName}، {day:00} {monthName} {year}",
                ShortGregorianDateFormat => $"{day:00} {monthName}",
                DayAndShortGregorianDateFormat => $"{dayName}، {day:00} {monthName}",
                _ => FallbackToGregorianFormat(date, formatCulture, gregorianFormat, "Persian")
            };
        }
        catch (ArgumentOutOfRangeException argEx)
        {
            Debug.WriteLine($"Persian calendar range error for date {date:O}: {argEx.Message}");
            return date.ToString(FallbackIsoDateFormat);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unexpected Persian date formatting error: {ex.Message}");
            return date.ToString(FallbackIsoDateFormat);
        }
    }

    private static string FormatGregorianDate(DateTime date, CultureInfo formatCulture, string gregorianFormat)
    {
        try
        {
            return date.ToString(gregorianFormat, formatCulture);
        }
        catch (FormatException formatEx)
        {
            Debug.WriteLine($"Gregorian date format error for date {date:O} with format '{gregorianFormat}': {formatEx.Message}");
            return date.ToString(FallbackIsoDateFormat);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unexpected Gregorian date formatting error: {ex.Message}");
            return date.ToString(FallbackIsoDateFormat);
        }
    }

    private static string FallbackToGregorianFormat(DateTime date, CultureInfo formatCulture, string requestedFormat, string calendarTypeName)
    {
        Debug.WriteLine($"FormatDate ({calendarTypeName}): Fallback standard formatting for requested format '{requestedFormat}'. May show Gregorian month names.");
        return date.ToString(requestedFormat, formatCulture);
    }

    private static string GetDayName(DayOfWeek? dayOfWeek, CultureInfo formatCulture)
    {
        if (dayOfWeek == null)
        {
            return string.Empty;
        }

        try
        {
            return formatCulture.DateTimeFormat.GetDayName(dayOfWeek.Value);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting day name for {dayOfWeek.Value} with culture {formatCulture.Name}: {ex.Message}");
            return dayOfWeek.Value.ToString();
        }
    }
}