using System.Diagnostics;

using Microsoft.Maui.Graphics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Tickly.Models;

namespace Tickly.Views.Plotting;

public class BarChartDrawable : IDrawable
{
    public List<PlotDataPoint> DataPoints { get; set; } = [];
    public Color TextColor { get; set; } = Colors.Grey;
    public float MaxValueFontSize { get; set; } = 10f;
    public float LabelFontSize { get; set; } = 8f;
    private const float minBarHeight = 1f; // Minimum height for zero-value bars
    private const float axisLabelPadding = 5f; // Padding between chart area and axis labels
    private const double MaxScaleValue = 1.0; // <<< Define constant for 100%

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // ... (initial debug logs remain the same) ...
        Debug.WriteLine($"BarChartDrawable.Draw: --- Drawing Started ---");
        Debug.WriteLine($"BarChartDrawable.Draw: DirtyRect: X={dirtyRect.X}, Y={dirtyRect.Y}, W={dirtyRect.Width}, H={dirtyRect.Height}");
        Debug.WriteLine($"BarChartDrawable.Draw: TextColor={TextColor}, DataPoints Count={(DataPoints?.Count ?? -1)}");

        // Log DataPoints passed to Draw
        Debug.WriteLine($"BarChartDrawable.Draw: Received DataPoints for drawing:");
        if (DataPoints != null)
        {
            for (int dpIdx = 0; dpIdx < DataPoints.Count; dpIdx++)
            {
                Debug.WriteLine($"  - DP[{dpIdx}]: Label='{DataPoints[dpIdx].Label}', Value={DataPoints[dpIdx].Value:F2}");
            }
        }
        else
        {
            Debug.WriteLine("  - DataPoints list is NULL");
        }


        if (DataPoints == null || !DataPoints.Any())
        {
            Debug.WriteLine("BarChartDrawable.Draw: No DataPoints found or DataPoints is null. Drawing 'No data' message.");
            canvas.StrokeColor = TextColor;
            canvas.FontSize = MaxValueFontSize;
            canvas.DrawString("No data to display", dirtyRect.Center.X, dirtyRect.Center.Y, 0, 0, HorizontalAlignment.Center, VerticalAlignment.Center);
            Debug.WriteLine("BarChartDrawable.Draw: --- Drawing Finished (No Data) ---");
            return;
        }

        var padding = 20f;
        var availableHeight = dirtyRect.Height - (2 * padding);
        var availableWidth = dirtyRect.Width - (2 * padding);

        Debug.WriteLine($"BarChartDrawable.Draw: availableHeight={availableHeight}, availableWidth={availableWidth}");
        Debug.WriteLine($"BarChartDrawable.Draw: MaxValueFontSize={MaxValueFontSize}, LabelFontSize={LabelFontSize}, axisLabelPadding={axisLabelPadding}");

        if (availableHeight <= 0 || availableWidth <= 0)
        {
            Debug.WriteLine($"BarChartDrawable.Draw: Not enough space after padding. Available H={availableHeight}, W={availableWidth}");
            Debug.WriteLine("BarChartDrawable.Draw: --- Drawing Finished (No Space) ---");
            return;
        }

        var chartHeight = availableHeight - MaxValueFontSize - LabelFontSize - axisLabelPadding;
        var chartWidth = availableWidth;

        Debug.WriteLine($"BarChartDrawable.Draw: Final chartHeight={chartHeight}, chartWidth={chartWidth}");

        var chartOriginX = dirtyRect.Left + padding;
        var chartOriginY = dirtyRect.Top + padding + MaxValueFontSize + axisLabelPadding;
        var chartBottomY = chartOriginY + chartHeight;

        if (DataPoints.Count == 0 || chartHeight <= 0 || chartWidth <= 0)
        {
            Debug.WriteLine($"BarChartDrawable.Draw: Calculated dimensions or DataPoints count is zero/negative. Cannot draw chart area. Count={DataPoints.Count}, chartH={chartHeight}, chartW={chartWidth}");
            canvas.StrokeColor = TextColor;
            canvas.FontSize = MaxValueFontSize;
            canvas.DrawString("Invalid chart dimensions or no data", dirtyRect.Center.X, dirtyRect.Center.Y, 0, 0, HorizontalAlignment.Center, VerticalAlignment.Center);
            Debug.WriteLine("BarChartDrawable.Draw: --- Drawing Finished (Invalid Dimensions/Count) ---");
            return;
        }

        var totalBarPlusSpacing = chartWidth / DataPoints.Count;
        var barWidth = totalBarPlusSpacing * 0.8f;
        var barSpacing = totalBarPlusSpacing - barWidth;

        Debug.WriteLine($"BarChartDrawable.Draw: Calculated - BarWidth={barWidth}, BarSpacing={barSpacing}, TotalBarPlusSpacing={totalBarPlusSpacing}");

        // <<< Use the constant MaxScaleValue for scaling and label >>>
        var maxValueForScaling = MaxScaleValue;
        Debug.WriteLine($"BarChartDrawable.Draw: Using Max Value for scaling = {maxValueForScaling}");

        // --- Draw Axis Labels ---
        canvas.FontColor = TextColor;
        canvas.FontSize = MaxValueFontSize;
        float maxValY = dirtyRect.Top + padding;
        float maxValX = dirtyRect.Left + padding;
        // <<< Use the constant MaxScaleValue for the label >>>
        Debug.WriteLine($"BarChartDrawable.Draw: Drawing MaxValue Label '{maxValueForScaling:P0}' at X={maxValX}, Y={maxValY}");
        canvas.DrawString($"{maxValueForScaling:P0}", maxValX, maxValY, 0, 0, HorizontalAlignment.Left, VerticalAlignment.Top);

        float zeroValY = chartBottomY;
        float zeroValX = dirtyRect.Left + padding;
        Debug.WriteLine($"BarChartDrawable.Draw: Drawing Zero Label '0%' at X={zeroValX}, Y={zeroValY}");
        canvas.DrawString("0%", zeroValX, zeroValY, 0, 0, HorizontalAlignment.Left, VerticalAlignment.Top);


        // --- Draw Bars and Labels ---
        Debug.WriteLine($"BarChartDrawable.Draw: Starting loop through {DataPoints.Count} data points.");
        for (int i = 0; i < DataPoints.Count; i++)
        {
            var dataPoint = DataPoints[i];
            // <<< Scale relative to MaxScaleValue (1.0) >>>
            var calculatedBarHeight = (float)(dataPoint.Value / maxValueForScaling * chartHeight);
            if (calculatedBarHeight < 0) calculatedBarHeight = 0;

            Debug.WriteLine($"BarChartDrawable.Draw: Index {i}: H_Calc = ({dataPoint.Value:F2} / {maxValueForScaling:F2}) * {chartHeight:F2} = {calculatedBarHeight:F2}");

            var barHeight = (calculatedBarHeight <= 0 && dataPoint.Value == 0) ? minBarHeight : calculatedBarHeight;
            if (barHeight < minBarHeight && barHeight > 0) barHeight = minBarHeight;

            var y = chartBottomY - barHeight;
            var x = chartOriginX + (i * totalBarPlusSpacing) + (barSpacing / 2);

            Debug.WriteLine($"BarChartDrawable.Draw: Index {i}: Label='{dataPoint.Label}', Value={dataPoint.Value}, Color={dataPoint.BarColor}");
            Debug.WriteLine($"BarChartDrawable.Draw: Index {i}: Final BarHeight={barHeight}, X={x}, Y={y}");


            canvas.FillColor = dataPoint.BarColor;
            if (barWidth > 0 && barHeight > 0)
            {
                Debug.WriteLine($"BarChartDrawable.Draw: Index {i}: Drawing Bar Rectangle at X={x}, Y={y}, W={barWidth}, H={barHeight}");
                canvas.FillRectangle(x, y, barWidth, barHeight);
            }
            else
            {
                Debug.WriteLine($"BarChartDrawable.Draw: Index {i}: Skipping bar drawing (barHeight={barHeight}, barWidth={barWidth}).");
            }

            float labelX = x + barWidth / 2;
            float labelY = chartBottomY + 2;
            canvas.FontSize = LabelFontSize;
            canvas.FontColor = TextColor;
            Debug.WriteLine($"BarChartDrawable.Draw: Index {i}: Drawing Label '{dataPoint.Label}' at X={labelX}, Y={labelY}");
            canvas.DrawString(dataPoint.Label, labelX, labelY, 0, 0, HorizontalAlignment.Center, VerticalAlignment.Top);
        }

        Debug.WriteLine("BarChartDrawable.Draw: --- Drawing Finished (Completed Loop) ---");
    }
}
