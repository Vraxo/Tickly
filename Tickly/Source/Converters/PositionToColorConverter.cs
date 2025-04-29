using System;
using System.Diagnostics; // Added for Debug.WriteLine
using System.Globalization;
using Microsoft.Maui.Graphics; // For Color

namespace Tickly.Converters;

public class PositionToColorConverter : IValueConverter
{
    // Define the start (top) and end (bottom) colors
    private static readonly Color StartColor = Colors.Red;    // Color for index 0
    private static readonly Color EndColor = Colors.LimeGreen; // Color for the last index

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int index = -1; // Default to invalid
        if (value is int idx)
        {
            index = idx;
        }
        else if (value != null)
        {
            Debug.WriteLine($"[PositionToColorConverter] Warning: Received non-int value: {value} ({value.GetType()})");
        }


        if (index < 0)
        {
            Debug.WriteLine($"[PositionToColorConverter] Error: Received invalid index: {index}");
            return Colors.Gray; // Return Gray for invalid index
        }

        int totalCount = 1; // Default to 1 if parameter is invalid
        if (parameter is int count && count > 0)
        {
            totalCount = count;
        }
        else if (parameter is string countStr && int.TryParse(countStr, out int parsedCount) && parsedCount > 0)
        {
            totalCount = parsedCount;
        }
        else if (parameter != null)
        {
            Debug.WriteLine($"[PositionToColorConverter] Warning: Received invalid parameter type: {parameter} ({parameter.GetType()}). Using default count 1.");
        }
        else
        {
            Debug.WriteLine($"[PositionToColorConverter] Warning: Received null parameter. Using default count 1.");
        }


        Debug.WriteLine($"[PositionToColorConverter] Index: {index}, TotalCount: {totalCount}"); // DEBUG OUTPUT

        // Handle the edge case of a single item
        if (totalCount <= 1)
        {
            Debug.WriteLine($"[PositionToColorConverter] Single item or invalid count <= 1. Returning StartColor: {StartColor}");
            return StartColor;
        }

        // Calculate the interpolation factor (0.0 at index 0, 1.0 at index totalCount-1)
        double factor = (double)index / (totalCount - 1);
        factor = Math.Clamp(factor, 0.0, 1.0); // Ensure factor stays within [0, 1]
        Debug.WriteLine($"[PositionToColorConverter] Factor: {factor:F3}");

        // Linear interpolation between StartColor and EndColor
        float r = (float)(StartColor.Red + factor * (EndColor.Red - StartColor.Red));
        float g = (float)(StartColor.Green + factor * (EndColor.Green - StartColor.Green));
        float b = (float)(StartColor.Blue + factor * (EndColor.Blue - StartColor.Blue));
        float a = (float)(StartColor.Alpha + factor * (EndColor.Alpha - StartColor.Alpha));

        Color resultColor = new Color(r, g, b, a);
        Debug.WriteLine($"[PositionToColorConverter] Result Color: R={r:F2}, G={g:F2}, B={b:F2}, A={a:F2}");

        return resultColor;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}