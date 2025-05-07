using Microsoft.Maui.Graphics;

namespace Tickly.Views.Plotting;

public readonly record struct PlotDataPoint(string Label, double Value, Color BarColor);