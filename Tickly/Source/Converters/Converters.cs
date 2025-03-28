// Converters/Converters.cs
using System;
using System.Globalization;
using System.Diagnostics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Tickly.Models;
using Tickly.Services; // For AppSettings

namespace Tickly.Converters;

/// <summary>
/// Converts TaskPriority enum to a specific Color.
/// </summary>
public class PriorityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
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

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

/// <summary>
/// Converts a TaskItem object into a display string describing its time/repetition,
/// respecting the selected calendar system setting and showing "Today"/"Tomorrow".
/// </summary>
public class TaskTimeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TaskItem task) return string.Empty;

        CalendarSystemType calendarSystem = AppSettings.SelectedCalendarSystem;
        CultureInfo formatCulture = calendarSystem == CalendarSystemType.Persian
                                    ? new CultureInfo("fa-IR")
                                    : CultureInfo.InvariantCulture;

        try
        {
            // Debugging (Optional - can be removed later)
            // Debug.WriteLine($"TaskTimeToStringConverter: Task='{task.Title}', Read Setting='{calendarSystem}', Culture='{formatCulture.Name}'");

            switch (task.TimeType)
            {
                case TaskTimeType.SpecificDate:
                    if (task.DueDate == null) return "No date";
                    // Format A: Used for specific, non-repeating dates
                    return FormatDate(task.DueDate.Value, calendarSystem, formatCulture, "ddd, dd MMM yyyy");

                case TaskTimeType.Repeating:
                    string repetition = task.RepetitionType switch
                    {
                        TaskRepetitionType.Daily => "Daily",
                        TaskRepetitionType.AlternateDay => "Every other day",
                        TaskRepetitionType.Weekly => $"Weekly on {GetDayName(task.RepetitionDayOfWeek, formatCulture)}",
                        _ => "Repeating"
                    };
                    string startDate = task.DueDate.HasValue
                                       // Format B: Used for the start date of repeating tasks
                                       ? $" (from {FormatDate(task.DueDate.Value, calendarSystem, formatCulture, "dd MMM")})"
                                       : "";
                    return $"{repetition}{startDate}";

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

    /// <summary>
    /// Helper method to format a DateTime object into a string.
    /// Checks for "Today" and "Tomorrow" before applying calendar-specific formatting.
    /// </summary>
    private string FormatDate(DateTime date, CalendarSystemType system, CultureInfo formatCulture, string gregorianFormat)
    {
        // Get today's date (ignoring time component)
        DateTime today = DateTime.Today;
        DateTime tomorrow = today.AddDays(1);

        // *** NEW: Check for Today and Tomorrow ***
        if (date.Date == today)
        {
            Debug.WriteLine($"FormatDate: Date {date:O} is Today.");
            return "Today"; // Return "Today" string
        }
        else if (date.Date == tomorrow)
        {
            Debug.WriteLine($"FormatDate: Date {date:O} is Tomorrow.");
            return "Tomorrow"; // Return "Tomorrow" string
        }
        // *** END NEW CHECK ***

        // --- If not Today or Tomorrow, proceed with existing formatting logic ---
        Debug.WriteLine($"FormatDate: Date {date:O} is not Today/Tomorrow. Proceeding with formatting...");

        // Debugging (Optional)
        // Debug.WriteLine($"FormatDate: Input Date='{date:O}', System='{system}', Culture='{formatCulture.Name}', RequestedFormat='{gregorianFormat}'");

        if (system == CalendarSystemType.Persian)
        {
            try
            {
                PersianCalendar pc = new PersianCalendar();
                int year = pc.GetYear(date);
                int month = pc.GetMonth(date);
                int day = pc.GetDayOfMonth(date);
                string dayName = formatCulture.DateTimeFormat.GetAbbreviatedDayName(pc.GetDayOfWeek(date));
                string monthName = formatCulture.DateTimeFormat.GetAbbreviatedMonthName(month);

                // Debugging (Optional)
                // Debug.WriteLine($"FormatDate (Persian Parts): Day={day}, Month={month}, MonthName='{monthName}', Year={year}");

                if (gregorianFormat == "ddd, dd MMM yyyy") // Format A
                {
                    // Debug.WriteLine("FormatDate (Persian): Matched format 'ddd, dd MMM yyyy'.");
                    return $"{dayName}، {day:00} {monthName} {year}";
                }
                if (gregorianFormat == "dd MMM") // Format B
                {
                    // Debug.WriteLine("FormatDate (Persian): Matched format 'dd MMM'.");
                    return $"{day:00} {monthName}";
                }

                Debug.WriteLine($"FormatDate (Persian): Fallback formatting for requested format '{gregorianFormat}'.");
                return date.ToString(formatCulture); // Fallback formatting
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Persian date formatting error: {ex.Message}");
                return date.ToString("yyyy-MM-dd"); // Error fallback
            }
        }
        else // Gregorian system requested
        {
            // Debug.WriteLine($"FormatDate (Gregorian): Using standard format '{gregorianFormat}' with culture '{formatCulture.Name}'.");
            return date.ToString(gregorianFormat, formatCulture);
        }
    }

    /// <summary>
    /// Helper method to get the full day name based on DayOfWeek and CultureInfo.
    /// </summary>
    private string GetDayName(DayOfWeek? dayOfWeek, CultureInfo formatCulture)
    {
        if (dayOfWeek == null) return string.Empty;
        try { return formatCulture.DateTimeFormat.GetDayName(dayOfWeek.Value); }
        catch (Exception ex) { Debug.WriteLine($"Error getting day name: {ex.Message}"); return dayOfWeek.Value.ToString(); }
    }


    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Simple converter to invert a boolean value.
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b ? !b : false;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b ? !b : false;
}