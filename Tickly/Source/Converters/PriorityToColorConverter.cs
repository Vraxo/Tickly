using System.Globalization;
using Tickly.Models;

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