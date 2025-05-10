using CommunityToolkit.Maui.Markup;
using Tickly.ViewModels;

namespace Tickly.Views;

public sealed partial class StatsPage : ContentPage
{
    private const double LargeFontSize = 20;
    private const double SmallFontSize = 12;
    private GraphicsView? _barChartView;

    public StatsPage(StatsViewModel viewModel)
    {
        BindingContext = viewModel;
        Title = "Stats";
        SetDynamicResource(BackgroundColorProperty, "AppBackgroundColor");

        Color initialChartTextColor = Application.Current?.RequestedTheme == AppTheme.Dark 
            ? Colors.WhiteSmoke
            : Colors.Black;

        viewModel.ChartDrawable.TextColor = initialChartTextColor;

        _barChartView = new GraphicsView
        { 
            HeightRequest = 200,
            Margin = new(0, 10, 0, 10)
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
                        FontSize = LargeFontSize,
                        FontAttributes = FontAttributes.Bold,
                        Margin = new(0, 0, 0, 5) 
                    },

                    new Label 
                    { 
                        Text = "Time Range:", 
                        Style = GetStyle("BaseLabelStyle"),
                        FontSize = SmallFontSize,
                        Margin = new(0,0,0,0)
                    },

                    new Picker
                    {
                        Title = "Select Time Range",
                        TitleColor = (Color)(Application.Current?.Resources["AppSecondaryTextColor"] ?? Colors.Gray)
                    }
                    .Bind(Picker.ItemsSourceProperty, nameof(StatsViewModel.PlotTimeRanges))
                    .Bind(Picker.SelectedItemProperty, nameof(StatsViewModel.SelectedPlotTimeRange)),

                    _barChartView,

                    new BoxView
                    { 
                        HeightRequest = 1,
                        BackgroundColor = Color.FromArgb("#333333"),
                        Margin = new(0, 15, 0, 15)
                    },

                    new Label 
                    { 
                        Text = "Export Daily Progress", 
                        FontSize = LargeFontSize,
                        FontAttributes = FontAttributes.Bold,
                        Margin = new(0, 0, 0, 10)
                    },
                    
                    new Label 
                    { 
                        Text = "Export your daily task completion percentages to a .txt file.",
                        Style = GetStyle("LightGrayLabel"),
                        FontSize = SmallFontSize,
                        LineBreakMode = LineBreakMode.WordWrap,
                        Margin = new(0,0,0,10)
                    },
                    
                    new Label
                    {
                        Text = "Sort Order:",
                        Style = GetStyle("BaseLabelStyle"),
                        FontSize = SmallFontSize,
                        Margin = new(0,5,0,0)
                    },
                    
                    new Picker
                    {
                        Title = "Select Sort Order",
                        TitleColor = (Color)(Application.Current?.Resources["AppSecondaryTextColor"] ?? Colors.Gray)
                    }
                    .Bind(Picker.ItemsSourceProperty, nameof(StatsViewModel.ExportSortOrders))
                    .Bind(Picker.SelectedItemProperty, nameof(StatsViewModel.SelectedExportSortOrder)),
                    
                    new Label
                    { 
                        Text = "Calendar For Dates:",
                        Style = GetStyle("BaseLabelStyle"),
                        FontSize = SmallFontSize,
                        Margin = new(0,10,0,0) 
                    },
                    
                    new Picker
                    {
                        Title = "Select Calendar Type",
                        TitleColor = (Color)(Application.Current?.Resources["AppSecondaryTextColor"] ?? Colors.Gray)
                    }
                    .Bind(Picker.ItemsSourceProperty, nameof(StatsViewModel.ExportCalendarTypes))
                    .Bind(Picker.SelectedItemProperty, nameof(StatsViewModel.SelectedExportCalendarType)),
                    
                    new Button
                    {
                        Text = "Export Daily Progress", BackgroundColor = Color.FromArgb("#3B71CA"), TextColor = Colors.WhiteSmoke, Margin = new(0, 15, 0, 0)
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

        _barChartView?.Invalidate();
    }

    private static Style GetStyle(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var resource) == true && resource is Style style)
        {
            return style;
        }

        return new(typeof(Label));
    }
}