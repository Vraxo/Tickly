using System;
using System.Globalization;
using Tickly.Models;

namespace Tickly.Converters;

public class IsTaskCompletableTodayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TaskItem task)
        {
            return false; // Or true depending on default behavior desired for non-tasks
        }

        // Non-repeating tasks are always completable
        if (task.TimeType != TaskTimeType.Repeating)
        {
            return true;
        }

        // Repeating tasks are completable only if they have a due date and it's today or earlier
        return task.DueDate.HasValue && task.DueDate.Value.Date <= DateTime.Today;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}