using CommunityToolkit.Maui.Markup;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Tickly.ViewModels;
using Tickly.Views.Plotting;

namespace Tickly.Views;

public sealed class StatsPage : ContentPage
{
    private const double LargeFontSize = 20;
    private const double SmallFontSize = 12;
    private GraphicsView? _barChartView;

    public StatsPage(StatsViewModel viewModel)
    {
        BindingContext = viewModel;
        Title = "Stats";
        // BackgroundColor set by Page style using DynamicResource

        // Determine initial text color for the chart based on the current theme
        Color initialChartTextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                                      ? Colors.WhiteSmoke // Example: Use WhiteSmoke for dark theme
                                      : Colors.Black;    // Example: Use Black for light theme

        viewModel.ChartDrawable.TextColor = initialChartTextColor; // Set initial color

        _barChartView = new GraphicsView
        {
            HeightRequest = 200,
            Margin = new Thickness(0, 10, 0, 10)
            // Drawable is bound below
        }
        .Bind(GraphicsView.DrawableProperty, nameof(StatsViewModel.ChartDrawable));

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 15,
                Children =
                {
                    new Label
                    {
                        Text = "Daily Progress Activity",
                        // Style = GetStyle("Headline"), // Use implicit or base label style
                        FontSize = LargeFontSize,
                        FontAttributes = FontAttributes.Bold,
                        Margin = new(0, 0, 0, 5)
                    },
                    new Label
                    {
                        Text = "Time Range:",
                        Style = GetStyle("BaseLabelStyle"), // Use base label style
                        FontSize = SmallFontSize,
                        Margin = new(0,0,0,0)
                    },
                    new Picker
                    {
                        Title = "Select Time Range",
                        // TextColor, BackgroundColor set by implicit Picker style
                        TitleColor = (Color)(Application.Current?.Resources["AppSecondaryTextColor"] ?? Colors.Gray) // Explicitly set TitleColor
                    }
                    .Bind(Picker.ItemsSourceProperty, nameof(StatsViewModel.PlotTimeRanges))
                    .Bind(Picker.SelectedItemProperty, nameof(StatsViewModel.SelectedPlotTimeRange)),

                    _barChartView,

                    new BoxView
                    {
                        HeightRequest = 1,
                        BackgroundColor = Color.FromArgb("#333333"), // Use a subtle separator color
                        Margin = new(0, 15, 0, 15)
                    },

                    new Label
                    {
                        Text = "Export Daily Progress",
                        // Style = GetStyle("Headline"), // Use implicit or base label style
                        FontSize = LargeFontSize,
                        FontAttributes = FontAttributes.Bold,
                        Margin = new(0, 0, 0, 10)
                    },
                    new Label
                    {
                        Text = "Export your daily task completion percentages to a .txt file.",
                        Style = GetStyle("LightGrayLabel"), // Use specific keyed style
                        FontSize = SmallFontSize,
                        LineBreakMode = LineBreakMode.WordWrap,
                        Margin = new(0,0,0,10)
                    },
                    new Label
                    {
                        Text = "Sort Order:",
                        Style = GetStyle("BaseLabelStyle"), // Use base label style
                        FontSize = SmallFontSize,
                        Margin = new(0,5,0,0)
                    },
                    new Picker
                    {
                        Title = "Select Sort Order",
                        // TextColor, BackgroundColor set by implicit Picker style
                        TitleColor = (Color)(Application.Current?.Resources["AppSecondaryTextColor"] ?? Colors.Gray) // Explicitly set TitleColor
                    }
                    .Bind(Picker.ItemsSourceProperty, nameof(StatsViewModel.ExportSortOrders))
                    .Bind(Picker.SelectedItemProperty, nameof(StatsViewModel.SelectedExportSortOrder)),
                    new Label
                    {
                        Text = "Calendar For Dates:",
                        Style = GetStyle("BaseLabelStyle"), // Use base label style
                        FontSize = SmallFontSize,
                        Margin = new(0,10,0,0)
                    },
                    new Picker
                    {
                        Title = "Select Calendar Type",
                        // TextColor, BackgroundColor set by implicit Picker style
                        TitleColor = (Color)(Application.Current?.Resources["AppSecondaryTextColor"] ?? Colors.Gray) // Explicitly set TitleColor
                    }
                    .Bind(Picker.ItemsSourceProperty, nameof(StatsViewModel.ExportCalendarTypes))
                    .Bind(Picker.SelectedItemProperty, nameof(StatsViewModel.SelectedExportCalendarType)),
                    new Button
                    {
                        Text = "Export Daily Progress",
                        BackgroundColor = Color.FromArgb("#3B71CA"), // Example button color
                        TextColor = Colors.WhiteSmoke,
                        Margin = new(0, 15, 0, 0)
                    }
                    .BindCommand(nameof(StatsViewModel.ExportProgressCommand))
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is StatsViewModel vm && vm.LoadProgressCommand.CanExecute(null))
        {
            await vm.LoadProgressCommand.ExecuteAsync(null);
        }
        // Ensure the chart redraws with potentially updated theme colors
        _barChartView?.Invalidate();
    }

    private static Style GetStyle(string key)
    {
        if (Application.Current != null && Application.Current.Resources.TryGetValue(key, out var resource) && resource is Style style)
        {
            return style;
        }
        // Fallback to default Label style if the specific keyed style isn't found
        return new Style(typeof(Label)); // Return a basic Label style if specific one not found
    }
}