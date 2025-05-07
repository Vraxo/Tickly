using CommunityToolkit.Maui.Markup;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Tickly.ViewModels;

namespace Tickly.Views;

public sealed class SettingsPage : ContentPage
{
    // Define font sizes for consistency
    private const double LargeFontSize = 20; // Example value for Large
    private const double SmallFontSize = 12; // Example value for Small

    public SettingsPage(SettingsViewModel viewModel) // ViewModel injected
    {
        BindingContext = viewModel; // Use injected ViewModel

        Title = "Settings";
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
                        Text = "Calendar Settings",
                        TextColor = Colors.WhiteSmoke,
                        FontSize = LargeFontSize, // Use numeric value
                        FontAttributes = FontAttributes.Bold,
                        Margin = new(0, 0, 0, 10)
                    },

                    new Label
                    {
                        Text = "Choose the calendar system for displaying dates:",
                        TextColor = Colors.LightGray,
                        FontSize = SmallFontSize // Use numeric value
                    },

                    new RadioButton
                    {
                        GroupName = "CalendarGroup",
                        Content = "Gregorian Calendar",
                        TextColor = Colors.WhiteSmoke
                    }
                    .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsGregorianSelected)),

                    new RadioButton
                    {
                        GroupName = "CalendarGroup",
                        Content = "Persian (Shamsi) Calendar",
                        TextColor = Colors.WhiteSmoke
                    }
                    .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsPersianSelected)),

                    new BoxView
                    {
                        HeightRequest = 1,
                        BackgroundColor = Color.FromArgb("#333333"),
                        Margin = new(0, 15, 0, 15)
                    },

                    new Label
                    {
                        Text = "Data Management",
                        TextColor = Colors.WhiteSmoke,
                        FontSize = LargeFontSize, // Use numeric value
                        FontAttributes = FontAttributes.Bold,
                        Margin = new(0, 0, 0, 10)
                    },

                    new Label
                    {
                        Text = "Export your current tasks to a JSON file or import tasks from a previously exported file (this will replace current tasks).",
                        TextColor = Colors.LightGray,
                        FontSize = SmallFontSize, // Use numeric value
                        LineBreakMode = LineBreakMode.WordWrap
                    },

                    new HorizontalStackLayout
                    {
                        Spacing = 10,
                        Margin = new(0, 10, 0, 0),
                        Children =
                        {
                            new Button
                            {
                                Text = "Export Tasks",
                                BackgroundColor = Color.FromArgb("#005A9C"),
                                TextColor = Colors.WhiteSmoke
                            }
                            .BindCommand(nameof(SettingsViewModel.ExportTasksCommand)),

                            new Button
                            {
                                Text = "Import Tasks",
                                BackgroundColor = Color.FromArgb("#008000"),
                                TextColor = Colors.WhiteSmoke
                            }
                            .BindCommand(nameof(SettingsViewModel.ImportTasksCommand))
                        }
                    },

                    // --- New Section for Exporting Daily Progress ---
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
                    .Bind(Picker.ItemsSourceProperty, nameof(SettingsViewModel.ExportSortOrders))
                    .Bind(Picker.SelectedItemProperty, nameof(SettingsViewModel.SelectedExportSortOrder)),

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
                    .Bind(Picker.ItemsSourceProperty, nameof(SettingsViewModel.ExportCalendarTypes))
                    .Bind(Picker.SelectedItemProperty, nameof(SettingsViewModel.SelectedExportCalendarType)),
                    
                    new Button
                    {
                        Text = "Export Daily Progress",
                        BackgroundColor = Color.FromArgb("#3B71CA"), // A different blue
                        TextColor = Colors.WhiteSmoke,
                        Margin = new(0, 15, 0, 0)
                    }
                    .BindCommand(nameof(SettingsViewModel.ExportProgressCommand))
                    // --- End New Section ---
                }
            }
        };
    }
}
