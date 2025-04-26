// Source/Converters/ProgressToColorConverter.cs
using System;
using System.Globalization;
using Microsoft.Maui.Graphics;

namespace Tickly.Converters;

public class ProgressToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double progress)
        {
            return Colors.Gray; // Default or error color
        }

        // Clamp progress between 0.0 and 1.0
        double clampedProgress = Math.Clamp(progress, 0.0, 1.0);

        // Linear interpolation between Red (0.0) and Green (1.0)
        // Red = (1, 0, 0)
        // Green = (0, 1, 0)
        float red = (float)(1.0 - clampedProgress);
        float green = (float)clampedProgress;
        float blue = 0.0f; // No blue component

        return Color.FromRgb(red, green, blue);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}