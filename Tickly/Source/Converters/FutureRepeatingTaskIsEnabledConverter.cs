using System.Globalization;
using Tickly.Models;

namespace Tickly.Converters;

public class FutureRepeatingTaskIsEnabledConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
            return true; // Default to enabled if bindings fail

        var timeTypeObj = values[0];
        var dueDateObj = values[1];

        if (timeTypeObj is not TaskTimeType timeType)
            return true; // Default to enabled if type is wrong

        // Task is enabled if it's NOT a repeating task
        if (timeType != TaskTimeType.Repeating)
            return true;

        // Task is enabled if it IS repeating but has NO due date (shouldn't happen?)
        if (dueDateObj is not DateTime dueDate)
            return true;

        // Task is enabled if it IS repeating AND its due date is today or earlier
        if (dueDate.Date <= DateTime.Today)
            return true;

        // Otherwise (Repeating Task with future DueDate), it is DISABLED
        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}