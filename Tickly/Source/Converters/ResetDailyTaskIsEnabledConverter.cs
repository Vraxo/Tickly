// Source/Converters/ResetDailyTaskIsEnabledConverter.cs
using System;
using System.Globalization;
using Tickly.Models;

namespace Tickly.Converters
{
    public sealed class ResetDailyTaskIsEnabledConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values?.Length >= 3 &&
                values[0] is TaskTimeType timeType &&
                values[1] is TaskRepetitionType repetitionType &&
                values[2] is DateTime dueDate)
            {
                // Enable only if it's Repeating, Daily, and the due date is exactly tomorrow
                return timeType == TaskTimeType.Repeating &&
                       repetitionType == TaskRepetitionType.Daily &&
                       dueDate.Date == DateTime.Today.AddDays(1);
            }

            return false; // Disable otherwise
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}