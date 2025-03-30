using System.Diagnostics;
using System.Globalization;
using Tickly.Models;
using Tickly.Services;

namespace Tickly.Converters;

public class PriorityToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TaskPriority priority)
        {
            return priority switch
            {
                TaskPriority.High => Colors.Red,
                TaskPriority.Medium => Colors.Orange,
                TaskPriority.Low => Colors.LimeGreen,
                _ => Colors.Gray,
            };
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TaskTimeToStringConverter : IValueConverter
{
    private static readonly string[] PersianAbbreviatedMonthNames = new[]
    {
        "", "فرور", "اردی", "خرد", "تیر", "مرد", "شهر", "مهر", "آبا", "آذر", "دی", "بهم", "اسف"
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TaskItem task)
        {
            return string.Empty;
        }

        CalendarSystemType calendarSystem = AppSettings.SelectedCalendarSystem;
        CultureInfo formatCulture = calendarSystem == CalendarSystemType.Persian
                                    ? new CultureInfo("fa-IR")
                                    : CultureInfo.InvariantCulture;

        try
        {
            switch (task.TimeType)
            {
                case TaskTimeType.SpecificDate:
                    if (task.DueDate == null) return "No date";
                    return FormatDate(task.DueDate.Value, calendarSystem, formatCulture, "ddd, dd MMM yyyy");

                case TaskTimeType.Repeating:
                    string repetition = task.RepetitionType switch
                    {
                        TaskRepetitionType.Daily => "Daily",
                        TaskRepetitionType.AlternateDay => "Every other day",
                        TaskRepetitionType.Weekly => $"Weekly on {GetDayName(task.RepetitionDayOfWeek, formatCulture)}",
                        _ => "Repeating"
                    };
                    string nextDueDateString = task.DueDate.HasValue
                                        ? FormatDate(task.DueDate.Value, calendarSystem, formatCulture, "ddd, dd MMM")
                                        : "Unknown";
                    return $"{repetition}, due {nextDueDateString}";

                case TaskTimeType.None:
                default:
                    return "Any time";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in TaskTimeToStringConverter for task '{task.Title}': {ex.Message}");
            return "Date Error";
        }
    }

    private string FormatDate(DateTime date, CalendarSystemType system, CultureInfo formatCulture, string gregorianFormat)
    {
        DateTime today = DateTime.Today;
        DateTime tomorrow = today.AddDays(1);

        if (date.Date == today)
        {
            return "Today";
        }
        else if (date.Date == tomorrow)
        {
            return "Tomorrow";
        }

        if (system == CalendarSystemType.Persian)
        {
            try
            {
                PersianCalendar pc = new PersianCalendar();
                int year = pc.GetYear(date);
                int month = pc.GetMonth(date);
                int day = pc.GetDayOfMonth(date);
                string dayName = formatCulture.DateTimeFormat.GetAbbreviatedDayName(pc.GetDayOfWeek(date));

                string monthName = (month >= 1 && month <= 12)
                                    ? PersianAbbreviatedMonthNames[month]
                                    : "?";

                if (gregorianFormat == "ddd, dd MMM yyyy")
                {
                    return $"{dayName}، {day:00} {monthName} {year}";
                }
                if (gregorianFormat == "dd MMM")
                {
                    return $"{day:00} {monthName}";
                }
                if (gregorianFormat == "ddd, dd MMM")
                {
                    return $"{dayName}، {day:00} {monthName}";
                }

                Debug.WriteLine($"FormatDate (Persian): Fallback standard formatting for requested format '{gregorianFormat}'. May show Gregorian month names.");
                return date.ToString(gregorianFormat, formatCulture);
            }
            catch (ArgumentOutOfRangeException argEx)
            {
                Debug.WriteLine($"Persian calendar range error for date {date:O}: {argEx.Message}");
                return date.ToString("yyyy-MM-dd");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected Persian date formatting error: {ex.Message}");
                return date.ToString("yyyy-MM-dd");
            }
        }
        else
        {
            return date.ToString(gregorianFormat, formatCulture);
        }
    }

    private string GetDayName(DayOfWeek? dayOfWeek, CultureInfo formatCulture)
    {
        if (dayOfWeek == null) return string.Empty;
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

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}