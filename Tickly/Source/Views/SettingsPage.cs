using CommunityToolkit.Maui.Markup;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Tickly.ViewModels;

namespace Tickly.Views;

public sealed class SettingsPage : ContentPage
{
    private const double LargeFontSize = 20;
    private const double SmallFontSize = 12;

    public SettingsPage(SettingsViewModel viewModel)
    {
        BindingContext = viewModel;

        Title = "Settings";

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
                        Text = "Theme Settings",
                        TextColor = Colors.WhiteSmoke,
                        FontSize = LargeFontSize,
                        FontAttributes = FontAttributes.Bold,
                        Margin = new(0, 0, 0, 10)
                    },
                    new Label
                    {
                        Text = "Choose the application theme:",
                        Style = GetStyle("LightGrayLabel")
                    },
                     new RadioButton
                    {
                        GroupName = "ThemeGroup",
                        Content = "Pitch Black"
                    }
                    .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsPitchBlackSelected)),

                    new RadioButton
                    {
                        GroupName = "ThemeGroup",
                        Content = "Dark Gray"
                    }
                    .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsDarkGraySelected)),

                    new BoxView
                    {
                        HeightRequest = 1,
                        BackgroundColor = Color.FromArgb("#333333"),
                        Margin = new(0, 15, 0, 15)
                    },

                    new Label
                    {
                        Text = "Calendar Settings",
                        TextColor = Colors.WhiteSmoke,
                        FontSize = LargeFontSize,
                        FontAttributes = FontAttributes.Bold,
                        Margin = new(0, 0, 0, 10)
                    },
                    new Label
                    {
                        Text = "Choose the calendar system for displaying dates:",
                        Style = GetStyle("LightGrayLabel")
                    },
                    new RadioButton
                    {
                        GroupName = "CalendarGroup",
                        Content = "Gregorian Calendar"
                    }
                    .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsGregorianSelected)),

                    new RadioButton
                    {
                        GroupName = "CalendarGroup",
                        Content = "Persian (Shamsi) Calendar"
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
                        FontSize = LargeFontSize,
                        FontAttributes = FontAttributes.Bold,
                        Margin = new(0, 0, 0, 10)
                    },
                    new Label
                    {
                        Text = "Export your current tasks to a JSON file or import tasks from a previously exported file (this will replace current tasks).",
                        Style = GetStyle("LightGrayLabel"),
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
                                BackgroundColor = Color.FromArgb("#4A6FA5"),
                                TextColor = Colors.WhiteSmoke
                            }
                            .BindCommand(nameof(SettingsViewModel.ExportTasksCommand)),

                            new Button
                            {
                                Text = "Import Tasks",
                                BackgroundColor = Color.FromArgb("#5A9A78"),
                                TextColor = Colors.WhiteSmoke
                            }
                            .BindCommand(nameof(SettingsViewModel.ImportTasksCommand))
                        }
                    }
                }
            }
        };
    }

    private static Style GetStyle(string key)
    {
        if (Application.Current != null && Application.Current.Resources.TryGetValue(key, out var resource) && resource is Style style)
        {
            return style;
        }

        return new Style(typeof(Label));
    }
}