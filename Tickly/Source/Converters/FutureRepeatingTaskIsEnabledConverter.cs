using System.Globalization;
using Tickly.Models;

namespace Tickly.Converters;

public sealed class FutureRepeatingTaskIsEnabledConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is [TaskTimeType.Repeating, DateTime dueDate, ..] && dueDate.Date > DateTime.Today)
        {
            return false;
        }

        return true;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}