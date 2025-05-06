// Utils/DateUtils.cs
using System;
using System.Globalization;

namespace Tickly.Utils;

public static class DateUtils
{
    /// <summary>
    /// Calculates the next occurrence of a specific DayOfWeek,
    /// starting from (and including) the given base date.
    /// </summary>
    /// <param name="baseDate">The date to start searching from.</param>
    /// <param name="targetDay">The desired DayOfWeek.</param>
    /// <returns>The DateTime of the next occurrence.</returns>
    public static DateTime GetNextWeekday(DateTime baseDate, DayOfWeek targetDay)
    {
        // Start checking from the base date itself
        DateTime nextDate = baseDate.Date; // Ensure we work with Date part only
        while (nextDate.DayOfWeek != targetDay)
        {
            nextDate = nextDate.AddDays(1);
        }
        return nextDate;
    }

    /// <summary>
    /// Calculates the next due date for a repeating task based on its
    /// current due date and repetition settings.
    /// </summary>
    public static DateTime? CalculateNextDueDate(Models.TaskItem task)
    {
        DateTime baseDate = task.DueDate ?? DateTime.Today; // Base calculation on current due date or today

        switch (task.RepetitionType)
        {
            case Models.TaskRepetitionType.Daily:
                return baseDate.AddDays(1).Date; // Ensure time is stripped

            case Models.TaskRepetitionType.AlternateDay:
                return baseDate.AddDays(2).Date; // Ensure time is stripped

            case Models.TaskRepetitionType.Weekly:
                if (task.RepetitionDayOfWeek.HasValue)
                {
                    // Find the next occurrence strictly *after* the current base date
                    DateTime nextDate = baseDate.AddDays(1);
                    return GetNextWeekday(nextDate, task.RepetitionDayOfWeek.Value);
                }
                else
                {
                    // Fallback: if no specific day, just add 7 days (unlikely with UI)
                    return baseDate.AddDays(7).Date;
                }

            default:
                return null; // Unknown repetition type
        }
    }

    /// <summary>
    /// Converts a DateTime object to its Persian (Shamsi) date representation.
    /// </summary>
    /// <param name="date">The Gregorian date to convert.</param>
    /// <returns>A string representing the date in "yyyy/MM/dd" format in Persian calendar.</returns>
    public static string ToPersianDateString(DateTime date)
    {
        PersianCalendar pc = new();
        return $"{pc.GetYear(date):0000}/{pc.GetMonth(date):00}/{pc.GetDayOfMonth(date):00}";
    }

    /// <summary>
    /// Converts a DateTime object to its Gregorian date representation.
    /// </summary>
    /// <param name="date">The date to convert.</param>
    /// <returns>A string representing the date in "yyyy/MM/dd" format in Gregorian calendar.</returns>
    public static string ToGregorianDateString(DateTime date)
    {
        return date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
    }
}
