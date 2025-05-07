using CommunityToolkit.Maui.Markup;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Tickly.ViewModels;

namespace Tickly.Views;

public sealed class StatsPage : ContentPage
{
    private const double LargeFontSize = 20;
    private const double SmallFontSize = 12;

    public StatsPage(StatsViewModel viewModel)
    {
        BindingContext = viewModel;
        Title = "Stats";
        BackgroundColor = Colors.Black;

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
}