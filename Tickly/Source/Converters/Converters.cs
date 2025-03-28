// Converters/Converters.cs
using System;
using System.Globalization;
using System.Diagnostics; // Keep for potential debugging
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
                _ => Colors.Gray, // Default fallback
            };
        }
        return Colors.Gray; // Default if input is not TaskPriority
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Conversion back is not implemented or needed for this scenario
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a TaskItem object into a display string describing its time/repetition,
/// respecting the selected calendar system setting and using relative terms like "Today" or "Tomorrow".
/// </summary>
public class TaskTimeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TaskItem task)
        {
            return string.Empty; // Return empty if the input is not a TaskItem
        }

        // Get the currently selected calendar system from static settings
        CalendarSystemType calendarSystem = AppSettings.SelectedCalendarSystem;
        // Determine the culture for formatting names and dates
        CultureInfo formatCulture = calendarSystem == CalendarSystemType.Persian
                                    ? new CultureInfo("fa-IR")
                                    : CultureInfo.InvariantCulture; // Use Invariant for consistent Gregorian formatting

        // Uncomment for debugging converter execution
        // Debug.WriteLine($"TaskTimeToStringConverter: Task='{task.Title}', Read Setting='{calendarSystem}', Culture='{formatCulture.Name}'");

        try
        {
            // Generate the display string based on the task's TimeType
            switch (task.TimeType)
            {
                case TaskTimeType.SpecificDate:
                    if (task.DueDate == null) return "No date"; // Handle null due date

                    // --- Relative Date Logic ---
                    DateTime today = DateTime.Today;
                    DateTime tomorrow = today.AddDays(1);

                    if (task.DueDate.Value.Date == today)
                    {
                        return "Today"; // Use "Today" if the due date is today
                    }
                    if (task.DueDate.Value.Date == tomorrow)
                    {
                        return "Tomorrow"; // Use "Tomorrow" if the due date is tomorrow
                    }
                    // --- End Relative Date Logic ---

                    // If not Today or Tomorrow, format the date fully
                    return FormatDate(task.DueDate.Value, calendarSystem, formatCulture, "ddd, dd MMM yyyy");

                case TaskTimeType.Repeating:
                    // Build the repetition description (e.g., "Daily", "Weekly on Tuesday")
                    string repetition = task.RepetitionType switch
                    {
                        TaskRepetitionType.Daily => "Daily",
                        TaskRepetitionType.AlternateDay => "Every other day",
                        TaskRepetitionType.Weekly => $"Weekly on {GetDayName(task.RepetitionDayOfWeek, formatCulture)}",
                        _ => "Repeating" // Fallback
                    };

                    // Format the *next* occurrence date using "dd MMM" format
                    string nextDateString = task.DueDate.HasValue
                                       ? FormatDate(task.DueDate.Value, calendarSystem, formatCulture, "dd MMM")
                                       : "No start date"; // Handle missing start date

                    // Combine repetition type and next date
                    return $"{repetition} (next: {nextDateString})";

                case TaskTimeType.None: // Explicitly handle the "Any time" case
                default: // Catches None or any unexpected/future TimeType values
                    return "Any time";
            }
        }
        catch (Exception ex)
        {
            // Log any errors that occur during conversion
            Debug.WriteLine($"Error in TaskTimeToStringConverter for task '{task.Title}': {ex.Message}");
            return "Date Error"; // Return an error indicator string
        }
    }

    /// <summary>
    /// Helper method to format a DateTime object into a string based on the
    /// selected calendar system and requested Gregorian format pattern.
    /// Handles specific formats manually for Persian for better control.
    /// </summary>
    private string FormatDate(DateTime date, CalendarSystemType system, CultureInfo formatCulture, string gregorianFormat)
    {
        // Uncomment for detailed format debugging
        // Debug.WriteLine($"FormatDate: Input Date='{date:O}', System='{system}', Culture='{formatCulture.Name}', RequestedFormat='{gregorianFormat}'");

        if (system == CalendarSystemType.Persian)
        {
            try
            {
                PersianCalendar pc = new PersianCalendar();
                int year = pc.GetYear(date);
                int month = pc.GetMonth(date);
                int day = pc.GetDayOfMonth(date);
                // Get abbreviated names using the Persian culture
                string dayName = formatCulture.DateTimeFormat.GetAbbreviatedDayName(pc.GetDayOfWeek(date));
                string monthName = formatCulture.DateTimeFormat.GetAbbreviatedMonthName(month);

                // Uncomment to check extracted Persian parts
                // Debug.WriteLine($"FormatDate (Persian Parts): Day={day}, Month={month}, MonthName='{monthName}', Year={year}");

                // Manually construct string based on requested Gregorian format pattern
                if (gregorianFormat == "ddd, dd MMM yyyy")
                {
                    // Debug.WriteLine("FormatDate (Persian): Matched format 'ddd, dd MMM yyyy'.");
                    return $"{dayName}، {day:00} {monthName} {year}";
                }
                if (gregorianFormat == "dd MMM")
                {
                    // Debug.WriteLine("FormatDate (Persian): Matched format 'dd MMM'.");
                    return $"{day:00} {monthName}"; // e.g., ۰۸ فروردین
                }

                // Fallback if the format string isn't one we handle manually
                Debug.WriteLine($"FormatDate (Persian): Fallback formatting for requested format '{gregorianFormat}'.");
                return date.ToString(formatCulture); // Use standard culture formatting
            }
            catch (ArgumentOutOfRangeException argEx)
            {
                Debug.WriteLine($"Persian calendar range error for date {date:O}: {argEx.Message}");
                return date.ToString("yyyy-MM-dd"); // Fallback format on error
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected Persian date formatting error: {ex.Message}");
                return date.ToString("yyyy-MM-dd"); // Fallback format on error
            }
        }
        else // Gregorian system requested
        {
            // Debug.WriteLine($"FormatDate (Gregorian): Using standard format '{gregorianFormat}' with culture '{formatCulture.Name}'.");
            // Use standard .NET formatting with the invariant culture for consistency
            return date.ToString(gregorianFormat, formatCulture);
        }
    }

    /// <summary>
    /// Helper method to get the full day name based on DayOfWeek and CultureInfo.
    /// </summary>
    private string GetDayName(DayOfWeek? dayOfWeek, CultureInfo formatCulture)
    {
        if (dayOfWeek == null) return string.Empty; // Return empty if no day is specified
        try
        {
            // Get the full day name using the specified culture
            return formatCulture.DateTimeFormat.GetDayName(dayOfWeek.Value);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting day name for {dayOfWeek.Value} with culture {formatCulture.Name}: {ex.Message}");
            return dayOfWeek.Value.ToString(); // Fallback to the enum name if error occurs
        }
    }


    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Conversion back is not implemented or needed for this scenario
        throw new NotImplementedException();
    }
}

/// <summary>
/// Simple converter to invert a boolean value. Useful for visibility bindings etc.
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Check if the input value is a boolean and return its inverse
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        // Return a default value (false) if input is not a boolean
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Invert back (same logic for simple negation)
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        // Return a default value if input is not a boolean
        return false;
    }
}