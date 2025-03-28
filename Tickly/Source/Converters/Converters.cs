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
                TaskPriority.Low => Colors.LimeGreen, // Using LimeGreen for better visibility on black
                _ => Colors.Gray,
            };
        }
        return Colors.Gray; // Default color if conversion fails
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Not needed for this application
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a TaskItem object into a display string describing its time/repetition,
/// respecting the selected calendar system setting and showing "Today"/"Tomorrow".
/// For repeating tasks, it shows the next due date.
/// </summary>
public class TaskTimeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Ensure the input value is a TaskItem
        if (value is not TaskItem task)
        {
            return string.Empty; // Return empty if not a TaskItem
        }

        // Get the currently selected calendar system
        CalendarSystemType calendarSystem = AppSettings.SelectedCalendarSystem;
        CultureInfo formatCulture = calendarSystem == CalendarSystemType.Persian
                                    ? new CultureInfo("fa-IR") // Use Persian culture for Persian calendar
                                    : CultureInfo.InvariantCulture; // Use Invariant for consistent Gregorian formatting

        // Optional Debugging
        // Debug.WriteLine($"TaskTimeToStringConverter: Task='{task.Title}', Read Setting='{calendarSystem}', Culture='{formatCulture.Name}'");

        try
        {
            // Generate the display string based on the task's TimeType
            switch (task.TimeType)
            {
                case TaskTimeType.SpecificDate:
                    if (task.DueDate == null) return "No date";
                    // Use full format for specific, non-repeating dates
                    return FormatDate(task.DueDate.Value, calendarSystem, formatCulture, "ddd, dd MMM yyyy");

                case TaskTimeType.Repeating:
                    // Build the repetition description part
                    string repetition = task.RepetitionType switch
                    {
                        TaskRepetitionType.Daily => "Daily",
                        TaskRepetitionType.AlternateDay => "Every other day",
                        TaskRepetitionType.Weekly => $"Weekly on {GetDayName(task.RepetitionDayOfWeek, formatCulture)}",
                        _ => "Repeating" // Default fallback
                    };
                    // *** MODIFIED: Show NEXT DUE DATE instead of "(from...)" ***
                    string nextDueDateString = task.DueDate.HasValue
                                        // Use FormatDate helper (handles Today/Tomorrow etc.)
                                        // Use a shorter, user-friendly format like "ddd, dd MMM"
                                        ? FormatDate(task.DueDate.Value, calendarSystem, formatCulture, "ddd, dd MMM")
                                        : "Unknown"; // Should always have a date if repeating
                    // Construct the final string, e.g., "Daily, due Today" or "Weekly on Sunday, due Sun, 30 Mar"
                    return $"{repetition}, due {nextDueDateString}";

                case TaskTimeType.None: // Explicitly handle None case
                default: // Catches None or any unexpected values
                    return "Any time";
            }
        }
        catch (Exception ex)
        {
            // Log errors during conversion
            Debug.WriteLine($"Error in TaskTimeToStringConverter for task '{task.Title}': {ex.Message}");
            return "Date Error"; // Display an error indicator in the UI
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

        // *** Check for Today and Tomorrow FIRST ***
        if (date.Date == today)
        {
            // Debug.WriteLine($"FormatDate: Date {date:O} is Today.");
            return "Today"; // Return "Today" string
        }
        else if (date.Date == tomorrow)
        {
            // Debug.WriteLine($"FormatDate: Date {date:O} is Tomorrow.");
            return "Tomorrow"; // Return "Tomorrow" string
        }

        // --- If not Today or Tomorrow, proceed with formatting ---
        // Debug.WriteLine($"FormatDate: Date {date:O} is not Today/Tomorrow. Proceeding...");

        if (system == CalendarSystemType.Persian)
        {
            try
            {
                PersianCalendar pc = new PersianCalendar();
                int year = pc.GetYear(date);
                int month = pc.GetMonth(date);
                int day = pc.GetDayOfMonth(date);
                // Get day/month names using the specific Persian culture (fa-IR)
                string dayName = formatCulture.DateTimeFormat.GetAbbreviatedDayName(pc.GetDayOfWeek(date));
                string monthName = formatCulture.DateTimeFormat.GetAbbreviatedMonthName(month);

                // Handle specific format requests manually for Persian
                if (gregorianFormat == "ddd, dd MMM yyyy") // Long format
                {
                    return $"{dayName}، {day:00} {monthName} {year}";
                }
                if (gregorianFormat == "dd MMM") // Short month format
                {
                    return $"{day:00} {monthName}";
                }
                if (gregorianFormat == "ddd, dd MMM") // Medium format (used for repeating tasks)
                {
                    return $"{dayName}، {day:00} {monthName}";
                }

                // Fallback if the requested format isn't explicitly handled above
                Debug.WriteLine($"FormatDate (Persian): Fallback formatting for requested format '{gregorianFormat}'.");
                return date.ToString(formatCulture);
            }
            catch (ArgumentOutOfRangeException argEx)
            {
                Debug.WriteLine($"Persian calendar range error for date {date:O}: {argEx.Message}");
                return date.ToString("yyyy-MM-dd"); // Fallback on error
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected Persian date formatting error: {ex.Message}");
                return date.ToString("yyyy-MM-dd"); // Fallback on error
            }
        }
        else // Gregorian system requested
        {
            // Use standard .NET formatting with the specified Gregorian format and culture (InvariantCulture)
            return date.ToString(gregorianFormat, formatCulture);
        }
    }

    /// <summary>
    /// Helper method to get the full day name based on DayOfWeek and CultureInfo.
    /// </summary>
    private string GetDayName(DayOfWeek? dayOfWeek, CultureInfo formatCulture)
    {
        if (dayOfWeek == null) return string.Empty;
        try
        {
            // Retrieve the day name using the specified culture's formatting info
            return formatCulture.DateTimeFormat.GetDayName(dayOfWeek.Value);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting day name for {dayOfWeek.Value} with culture {formatCulture.Name}: {ex.Message}");
            return dayOfWeek.Value.ToString(); // Fallback to enum name on error
        }
    }


    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Not needed for this application
        throw new NotImplementedException();
    }
}

/// <summary>
/// Simple converter to invert a boolean value. Useful for visibility bindings.
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b ? !b : false;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b ? !b : false;
}