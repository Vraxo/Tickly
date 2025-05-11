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
        this.SetDynamicResource(BackgroundColorProperty, "AppBackgroundColor");

        Color initialChartTextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.WhiteSmoke : Colors.Black;
        if (viewModel.ChartDrawable != null)
        {
            viewModel.ChartDrawable.TextColor = initialChartTextColor;
        }

        _barChartView = new GraphicsView { HeightRequest = 200, Margin = new Thickness(0, 10, 0, 10) }
            .Bind(GraphicsView.DrawableProperty, nameof(StatsViewModel.ChartDrawable));

        var resetProgressButton = new Button
        {
            Text = "Reset All Progress",
            BackgroundColor = Color.FromHex("#BF616A"),
            TextColor = Colors.WhiteSmoke,
            Margin = new Thickness(0, 20, 0, 0)
        }.BindCommand(nameof(StatsViewModel.ResetProgressCommand));

        var mainLayout = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 15,
            Children =
            {
                new Label { Text = "Daily Progress Activity", FontSize = LargeFontSize, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 0, 0, 5) },
                new Label { Text = "Time Range:", Style = GetStyle("BaseLabelStyle"), FontSize = SmallFontSize, Margin = new Thickness(0,0,0,0) },
                new Picker
                {
                    Title = "Select Time Range"
                }
                .DynamicResource(Picker.TitleColorProperty, "AppSecondaryTextColor")
                .Bind(Picker.ItemsSourceProperty, nameof(StatsViewModel.PlotTimeRanges))
                .Bind(Picker.SelectedItemProperty, nameof(StatsViewModel.SelectedPlotTimeRange)),

                _barChartView,

                new BoxView { HeightRequest = 1, Margin = new Thickness(0, 15, 0, 15) }
                    .DynamicResource(BoxView.ColorProperty, "AppSecondaryTextColor"),

                new Label { Text = "Export Daily Progress", FontSize = LargeFontSize, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 0, 0, 10) },
                new Label { Text = "Export your daily task completion percentages to a .txt file.", Style = GetStyle("LightGrayLabel"), FontSize = SmallFontSize, LineBreakMode = LineBreakMode.WordWrap, Margin = new Thickness(0,0,0,10) },
                new Label { Text = "Sort Order:", Style = GetStyle("BaseLabelStyle"), FontSize = SmallFontSize, Margin = new Thickness(0,5,0,0) },
                new Picker
                {
                    Title = "Select Sort Order"
                }
                .DynamicResource(Picker.TitleColorProperty, "AppSecondaryTextColor")
                .Bind(Picker.ItemsSourceProperty, nameof(StatsViewModel.ExportSortOrders))
                .Bind(Picker.SelectedItemProperty, nameof(StatsViewModel.SelectedExportSortOrder)),

                new Label { Text = "Calendar For Dates:", Style = GetStyle("BaseLabelStyle"), FontSize = SmallFontSize, Margin = new Thickness(0,10,0,0) },
                new Picker
                {
                    Title = "Select Calendar Type"
                }
                .DynamicResource(Picker.TitleColorProperty, "AppSecondaryTextColor")
                .Bind(Picker.ItemsSourceProperty, nameof(StatsViewModel.ExportCalendarTypes))
                .Bind(Picker.SelectedItemProperty, nameof(StatsViewModel.SelectedExportCalendarType)),

                new Button
                {
                    Text = "Export Daily Progress", Margin = new Thickness(0, 15, 0, 0)
                }
                .DynamicResource(Button.BackgroundColorProperty, "AppPrimaryActionBackgroundColor")
                .DynamicResource(Button.TextColorProperty, "AppPrimaryActionForegroundColor")
                .BindCommand(nameof(StatsViewModel.ExportProgressCommand)),

                new BoxView { HeightRequest = 1, Margin = new Thickness(0, 20, 0, 15) }
                    .DynamicResource(BoxView.ColorProperty, "AppSecondaryTextColor"),

                new Label { Text = "Danger Zone", FontSize = LargeFontSize, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 0, 0, 10) }
                    .DynamicResource(Label.TextColorProperty, "NordAurora0"),

                resetProgressButton
            }
        };

        Content = new ScrollView { Content = mainLayout };
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

        var fallbackStyle = new Style(typeof(Label));
        fallbackStyle.Setters.Add(new Setter { Property = Label.FontSizeProperty, Value = 14 });
        if (Application.Current?.Resources.TryGetValue("AppForegroundColor", out var fgColor) == true && fgColor is Color color)
        {
            fallbackStyle.Setters.Add(new Setter { Property = Label.TextColorProperty, Value = color });
        }
        else
        {
            fallbackStyle.Setters.Add(new Setter { Property = Label.TextColorProperty, Value = Colors.Black });
        }
        return fallbackStyle;
    }
}