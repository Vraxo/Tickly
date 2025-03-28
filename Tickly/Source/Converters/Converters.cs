// Converters/Converters.cs
using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Tickly.Models;
using Tickly.Services; // For AppSettings

namespace Tickly.Converters;

// PriorityToColorConverter definition...
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

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


public class TaskTimeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TaskItem task)
        {
            return string.Empty;
        }

        // Get the currently selected calendar system
        CalendarSystemType calendarSystem = AppSettings.SelectedCalendarSystem;
        CultureInfo formatCulture = calendarSystem == CalendarSystemType.Persian
                                    ? new CultureInfo("fa-IR")
                                    : CultureInfo.InvariantCulture;

        try
        {
            // *** Add Diagnostic Debugging Line Here if needed ***
            // System.Diagnostics.Debug.WriteLine($"Converter Input: Task='{task.Title}', TimeType='{task.TimeType}', DueDate='{task.DueDate}'");

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
                    // Repeating tasks also use DueDate as their start date
                    string startDate = task.DueDate.HasValue
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
            System.Diagnostics.Debug.WriteLine($"Error in TaskTimeToStringConverter: {ex.Message}");
            return "Date Error"; // Indicate an error occurred during formatting
        }
    }

    // Helper method to format dates based on selected calendar
    private string FormatDate(DateTime date, CalendarSystemType system, CultureInfo formatCulture, string gregorianFormat)
    {
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

                if (gregorianFormat == "ddd, dd MMM yyyy")
                {
                    return $"{dayName}، {day:00} {monthName} {year}";
                }
                if (gregorianFormat == "dd MMM")
                {
                    return $"{day:00} {monthName}";
                }
                return date.ToString(formatCulture); // Fallback
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Persian date formatting error: {ex.Message}");
                return date.ToString("yyyy-MM-dd"); // Fallback
            }
        }
        else // Gregorian
        {
            return date.ToString(gregorianFormat, formatCulture);
        }
    }

    // Helper method to get day name respecting culture
    private string GetDayName(DayOfWeek? dayOfWeek, CultureInfo formatCulture)
    {
        if (dayOfWeek == null) return string.Empty;
        try
        {
            return formatCulture.DateTimeFormat.GetDayName(dayOfWeek.Value);
        }
        catch
        {
            return dayOfWeek.Value.ToString(); // Fallback
        }
    }


    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// InverseBooleanConverter definition...
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false; // Or throw an exception / return a default
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false; // Or throw an exception / return a default
    }
}