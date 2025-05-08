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
                        // Style = GetStyle("Headline"), // Use implicit style
                        FontSize = LargeFontSize,
                        FontAttributes = FontAttributes.Bold,
                        Margin = new(0, 0, 0, 10)
                    },
                    new Label
                    {
                        Text = "Choose the application theme:",
                        Style = GetStyle("LightGrayLabel") // Keep explanatory text style consistent
                    },
                    new RadioButton
                    {
                        GroupName = "ThemeGroup",
                        Content = "Light" // Add Light option
                    }
                    .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsLightSelected)),
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

                    new RadioButton
                    {
                        GroupName = "ThemeGroup",
                        Content = "Nord"
                    }
                    .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsNordSelected)),

                    new BoxView
                    {
                        HeightRequest = 1,
                        BackgroundColor = Color.FromArgb("#333333"), // Use a dynamic separator?
                        Margin = new(0, 15, 0, 15)
                    },

                    new Label
                    {
                        Text = "Calendar Settings",
                        // Style = GetStyle("Headline"), // Use implicit style
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
                        BackgroundColor = Color.FromArgb("#333333"), // Use a dynamic separator?
                        Margin = new(0, 15, 0, 15)
                    },

                    new Label
                    {
                        Text = "Data Management",
                        // Style = GetStyle("Headline"), // Use implicit style
                        FontSize = LargeFontSize,
                        FontAttributes = FontAttributes.Bold,
                        Margin = new(0, 0, 0, 10)
                    },
                    new Label
                    {
                        Text = "Export your current data (tasks, settings, progress) to a JSON file, or import data from a previously exported file. Importing will replace all current tasks, settings, and progress.",
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
                                Text = "Export Data", // Changed text
                                BackgroundColor = Color.FromArgb("#4A6FA5"), 
                                TextColor = Colors.WhiteSmoke
                            }
                            .BindCommand(nameof(SettingsViewModel.ExportDataCommand)), // Changed command

                            new Button
                            {
                                Text = "Import Data", // Changed text
                                BackgroundColor = Color.FromArgb("#5A9A78"), 
                                TextColor = Colors.WhiteSmoke
                            }
                            .BindCommand(nameof(SettingsViewModel.ImportDataCommand)) // Changed command
                        }
                    }
                }
            }
        };
    }

    // Helper to get styles safely
    private static Style GetStyle(string key)
    {
        // Attempt to find the specific style
        if (Application.Current != null && Application.Current.Resources.TryGetValue(key, out var resource) && resource is Style style)
        {
            return style;
        }

        // Fallback to the base Label style if the specific key isn't found
        if (Application.Current != null && Application.Current.Resources.TryGetValue("BaseLabelStyle", out var baseResource) && baseResource is Style baseStyle)
        {
            return baseStyle;
        }

        // Absolute fallback if even BaseLabelStyle is missing
        return new Style(typeof(Label));
    }
}
