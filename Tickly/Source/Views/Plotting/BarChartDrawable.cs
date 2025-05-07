using Microsoft.Maui.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace Tickly.Views.Plotting;

public sealed class BarChartDrawable : IDrawable
{
    public List<PlotDataPoint> DataPoints { get; set; } = [];
    public Color TextColor { get; set; } = Colors.Black;

    private const float BarMargin = 5f;
    private const float LabelAreaHeight = 30f;
    private const float ChartAreaPadding = 10f;
    private const float MaxBarLabelFontSize = 10f;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (DataPoints == null || !DataPoints.Any())
        {
            canvas.StrokeColor = TextColor;
            canvas.FontSize = 12;
            canvas.DrawString("No progress data available.", dirtyRect, HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }

        float chartAreaWidth = dirtyRect.Width - 2 * ChartAreaPadding;
        float chartAreaHeight = dirtyRect.Height - LabelAreaHeight - 2 * ChartAreaPadding;
        float chartAreaX = dirtyRect.X + ChartAreaPadding;
        float chartAreaY = dirtyRect.Y + ChartAreaPadding;

        int numPoints = DataPoints.Count;
        float totalBarWidthAvailable = chartAreaWidth - (numPoints + 1) * BarMargin;
        float barWidth = totalBarWidthAvailable / numPoints;
        barWidth = Math.Max(1f, barWidth); // Ensure bar has at least 1px width

        double maxValue = 1.0;

        canvas.StrokeSize = 1;
        canvas.FontColor = TextColor;
        canvas.FontSize = (float)Math.Min(MaxBarLabelFontSize, Math.Max(6.0, barWidth * 0.6)); // Adjust font size based on bar width

        float currentX = chartAreaX + BarMargin;

        for (int i = 0; i < numPoints; i++)
        {
            var point = DataPoints[i];
            var barHeight = (float)(point.Value / maxValue * chartAreaHeight);
            barHeight = Math.Max(0f, barHeight); // Ensure non-negative height

            var barRect = new RectF(currentX, chartAreaY + chartAreaHeight - barHeight, barWidth, barHeight);

            canvas.FillColor = point.BarColor;
            canvas.FillRectangle(barRect);

            canvas.FontColor = TextColor;
            canvas.DrawString(point.Label, currentX, chartAreaY + chartAreaHeight + 5, barWidth, LabelAreaHeight - 10, HorizontalAlignment.Center, VerticalAlignment.Top);

            currentX += barWidth + BarMargin;
        }

        canvas.StrokeColor = TextColor.WithAlpha(0.5f);
        canvas.DrawLine(chartAreaX, chartAreaY + chartAreaHeight, chartAreaX + chartAreaWidth, chartAreaY + chartAreaHeight);
    }
}