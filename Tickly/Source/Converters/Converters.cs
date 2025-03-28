// Converters/Converters.cs
using System;
using System.Globalization;
using System.Diagnostics; // <<< Added for Debug.WriteLine
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
/// respecting the selected calendar system setting.
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

        // *** DEBUG POINT 1: Check the setting value when converter runs ***
        CalendarSystemType calendarSystem = AppSettings.SelectedCalendarSystem;
        Debug.WriteLine($"TaskTimeToStringConverter: Task='{task.Title}', Read Setting='{calendarSystem}'");

        // Determine the culture to use for formatting based on the setting
        CultureInfo formatCulture = calendarSystem == CalendarSystemType.Persian
                                    ? new CultureInfo("fa-IR") // Use Persian culture for Persian calendar
                                    : CultureInfo.InvariantCulture; // Use Invariant for consistent Gregorian formatting

        // *** DEBUG POINT 2: Check the determined culture ***
        Debug.WriteLine($"TaskTimeToStringConverter: Determined Culture='{formatCulture.Name}'");

        try
        {
            // Generate the display string based on the task's TimeType
            switch (task.TimeType)
            {
                case TaskTimeType.SpecificDate:
                    if (task.DueDate == null) return "No date";
                    // Format A: Used for specific, non-repeating dates
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
                    // Add the start date part if available
                    string startDate = task.DueDate.HasValue
                                       // Format B: Used for the start date of repeating tasks
                                       ? $" (from {FormatDate(task.DueDate.Value, calendarSystem, formatCulture, "dd MMM")})"
                                       : "";
                    return $"{repetition}{startDate}";

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
    /// Helper method to format a DateTime object into a string based on the
    /// selected calendar system and requested Gregorian format pattern.
    /// </summary>
    private string FormatDate(DateTime date, CalendarSystemType system, CultureInfo formatCulture, string gregorianFormat)
    {
        // *** DEBUG POINT 3: Check input parameters to FormatDate ***
        Debug.WriteLine($"FormatDate: Input Date='{date:O}', System='{system}', Culture='{formatCulture.Name}', RequestedFormat='{gregorianFormat}'");

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

                // *** DEBUG POINT 4: Check extracted Persian date components ***
                Debug.WriteLine($"FormatDate (Persian Parts): Day={day}, Month={month}, MonthName='{monthName}', Year={year}");

                // Handle specific format requests manually for Persian
                if (gregorianFormat == "ddd, dd MMM yyyy") // Format A
                {
                    Debug.WriteLine("FormatDate (Persian): Matched format 'ddd, dd MMM yyyy'.");
                    // Example output: شنبه، ۰۱ فروردین ۱۴۰۳
                    return $"{dayName}، {day:00} {monthName} {year}";
                }
                if (gregorianFormat == "dd MMM") // Format B
                {
                    Debug.WriteLine("FormatDate (Persian): Matched format 'dd MMM'.");
                    // Example output: ۰۸ فروردین (for 28 March)
                    return $"{day:00} {monthName}";
                }

                // Fallback if the requested format isn't explicitly handled above
                Debug.WriteLine($"FormatDate (Persian): Fallback formatting for requested format '{gregorianFormat}'.");
                // This might produce YYYY/MM/DD depending on the 'fa-IR' culture's default patterns
                return date.ToString(formatCulture);
            }
            catch (ArgumentOutOfRangeException argEx)
            {
                // Catch specific errors related to calendar calculations if date is outside supported range
                Debug.WriteLine($"Persian calendar range error for date {date:O}: {argEx.Message}");
                return date.ToString("yyyy-MM-dd"); // Fallback to ISO format on error
            }
            catch (Exception ex)
            {
                // Catch any other unexpected errors during formatting
                Debug.WriteLine($"Unexpected Persian date formatting error: {ex.Message}");
                return date.ToString("yyyy-MM-dd"); // Fallback to ISO format on error
            }
        }
        else // Gregorian system requested
        {
            Debug.WriteLine($"FormatDate (Gregorian): Using standard format '{gregorianFormat}' with culture '{formatCulture.Name}'.");
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
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Check if the input value is a boolean and invert it
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        // Return a default value (e.g., false) if input is not a boolean
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Invert back if needed (same logic for simple negation)
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        // Return a default value if input is not a boolean
        return false;
    }
}