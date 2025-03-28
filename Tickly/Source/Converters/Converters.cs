// Converters/Converters.cs
using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Tickly.Models;

namespace Tickly.Converters;

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
        if (value is TaskItem task)
        {
            switch (task.TimeType)
            {
                case TaskTimeType.SpecificDate:
                    return task.DueDate?.ToString("ddd, dd MMM yyyy") ?? "No date";
                case TaskTimeType.Repeating:
                    string repetition = task.RepetitionType switch
                    {
                        TaskRepetitionType.Daily => "Daily",
                        TaskRepetitionType.AlternateDay => "Every other day",
                        TaskRepetitionType.Weekly => $"Weekly on {task.RepetitionDayOfWeek}",
                        _ => "Repeating"
                    };
                    string startDate = task.DueDate.HasValue ? $" (from {task.DueDate:dd MMM})" : "";
                    return $"{repetition}{startDate}";
                case TaskTimeType.None:
                default:
                    return "Any time";
            }
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Simple Boolean Negation Converter (useful for hiding/showing elements)
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !(bool)value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !(bool)value;
    }
}