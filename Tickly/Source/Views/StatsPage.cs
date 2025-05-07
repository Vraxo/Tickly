using CommunityToolkit.Maui.Markup;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Tickly.ViewModels;
using Tickly.Views.Plotting; // Required for GraphicsView

namespace Tickly.Views;

public sealed class StatsPage : ContentPage
{
    private const double LargeFontSize = 20;
    private const double SmallFontSize = 12;
    private GraphicsView? _barChartView; // Keep a reference

    public StatsPage(StatsViewModel viewModel)
    {
        BindingContext = viewModel;
        Title = "Stats";
        BackgroundColor = Colors.Black;

        _barChartView = new GraphicsView
        {
            HeightRequest = 200,
            Margin = new Thickness(0, 10, 0, 10),
            Drawable = viewModel.ChartDrawable // Initially set
        }
        .Bind(GraphicsView.DrawableProperty, nameof(StatsViewModel.ChartDrawable)); // Bind for updates

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
                        TextColor = Colors.WhiteSmoke,
                        FontSize = LargeFontSize,
                        FontAttributes = FontAttributes.Bold,
                        Margin = new(0, 0, 0, 5)
                    },
                    new Label
                    {
                        Text = "Time Range:",
                        TextColor = Colors.WhiteSmoke,
                        FontSize = SmallFontSize,
                        Margin = new(0,0,0,0)
                    },
                    new Picker
                    {
                        Title = "Select Time Range",
                        TextColor = Colors.WhiteSmoke,
                        TitleColor = Colors.LightGray,
                        BackgroundColor = Color.FromArgb("#1e1e1e")
                    }
                    .Bind(Picker.ItemsSourceProperty, nameof(StatsViewModel.PlotTimeRanges))
                    .Bind(Picker.SelectedItemProperty, nameof(StatsViewModel.SelectedPlotTimeRange)),

                    _barChartView, // Add the GraphicsView here

                    new BoxView
                    {
                        HeightRequest = 1,
                        BackgroundColor = Color.FromArgb("#333333"),
                        Margin = new(0, 15, 0, 15)
                    },

                    new Label
                    {
                        Text = "Export Daily Progress",
                        TextColor = Colors.WhiteSmoke,
                        FontSize = LargeFontSize,
                        FontAttributes = FontAttributes.Bold,
                        Margin = new(0, 0, 0, 10)
                    },
                    new Label
                    {
                        Text = "Export your daily task completion percentages to a .txt file.",
                        TextColor = Colors.LightGray,
                        FontSize = SmallFontSize,
                        LineBreakMode = LineBreakMode.WordWrap,
                        Margin = new(0,0,0,10)
                    },
                    new Label
                    {
                        Text = "Sort Order:",
                        TextColor = Colors.WhiteSmoke,
                        FontSize = SmallFontSize,
                        Margin = new(0,5,0,0)
                    },
                    new Picker
                    {
                        Title = "Select Sort Order",
                        TextColor = Colors.WhiteSmoke,
                        TitleColor = Colors.LightGray,
                        BackgroundColor = Color.FromArgb("#1e1e1e")
                    }
                    .Bind(Picker.ItemsSourceProperty, nameof(StatsViewModel.ExportSortOrders))
                    .Bind(Picker.SelectedItemProperty, nameof(StatsViewModel.SelectedExportSortOrder)),
                    new Label
                    {
                        Text = "Calendar For Dates:",
                        TextColor = Colors.WhiteSmoke,
                        FontSize = SmallFontSize,
                        Margin = new(0,10,0,0)
                    },
                    new Picker
                    {
                        Title = "Select Calendar Type",
                        TextColor = Colors.WhiteSmoke,
                        TitleColor = Colors.LightGray,
                        BackgroundColor = Color.FromArgb("#1e1e1e")
                    }
                    .Bind(Picker.ItemsSourceProperty, nameof(StatsViewModel.ExportCalendarTypes))
                    .Bind(Picker.SelectedItemProperty, nameof(StatsViewModel.SelectedExportCalendarType)),
                    new Button
                    {
                        Text = "Export Daily Progress",
                        BackgroundColor = Color.FromArgb("#3B71CA"),
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
        // Invalidate the GraphicsView to ensure it redraws when the page appears
        _barChartView?.Invalidate();
    }
}