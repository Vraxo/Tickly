using CommunityToolkit.Maui.Markup;
using Tickly.ViewModels;

namespace Tickly.Views;

public sealed class SettingsPage : ContentPage
{
    private const double LargeFontSize = 20;

    public SettingsPage(SettingsViewModel viewModel)
    {
        BindingContext = viewModel;
        Title = "Settings";
        SetDynamicResource(BackgroundColorProperty, "AppBackgroundColor");

        VerticalStackLayout mainLayout = new()
        {
            Padding = 20,
            Spacing = 15,
            Children =
            {
                new Label
                {
                    Text = "Theme Settings",
                    FontSize = LargeFontSize,
                    FontAttributes = FontAttributes.Bold,
                    Margin = new(0, 0, 0, 10)
                },
                
                new Label
                {
                    Text = "Choose the application theme:",
                    Style = GetStyle("LightGrayLabel"),
                    Margin = new(0,0,0,5)
                },

                new Label 
                {
                    Text = "Dark Themes",
                    FontAttributes = FontAttributes.Bold,
                    Margin = new(0, 10, 0, 5)
                },

                new RadioButton 
                {
                    GroupName = "ThemeGroup",
                    Content = "Pitch Black",
                    BackgroundColor = Colors.Transparent
                }
                .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsPitchBlackSelected)),

                new RadioButton 
                { 
                    GroupName = "ThemeGroup",
                    Content = "Dark Gray",
                    BackgroundColor = Colors.Transparent
                }.
                Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsDarkGraySelected)),
                
                new RadioButton 
                {
                    GroupName = "ThemeGroup",
                    Content = "Nord",
                    BackgroundColor = Colors.Transparent
                }
                .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsNordSelected)),
                
                new RadioButton 
                { 
                    GroupName = "ThemeGroup",
                    Content = "Catppuccin Mocha",
                    BackgroundColor = Colors.Transparent
                }
                .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsCatppuccinMochaSelected)),
                
                new RadioButton 
                { 
                    GroupName = "ThemeGroup",
                    Content = "Solarized Dark",
                    BackgroundColor = Colors.Transparent
                }
                .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsSolarizedDarkSelected)),
                
                new RadioButton 
                { 
                    GroupName = "ThemeGroup",
                    Content = "Gruvbox Dark",
                    BackgroundColor = Colors.Transparent
                }
                .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsGruvboxDarkSelected)),
                
                new RadioButton 
                { 
                    GroupName = "ThemeGroup",
                    Content = "Monokai",
                    BackgroundColor = Colors.Transparent
                }
                .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsMonokaiSelected)),

                new Label
                {
                    Text = "Light Themes",
                    FontAttributes = FontAttributes.Bold,
                    Margin = new(0, 15, 0, 5)
                },
                
                new RadioButton
                {
                    GroupName = "ThemeGroup",
                    Content = "Default Light",
                    BackgroundColor = Colors.Transparent
                }
                .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsLightSelected)),
                
                new RadioButton
                {
                    GroupName = "ThemeGroup",
                    Content = "Solarized Light",
                    BackgroundColor = Colors.Transparent
                }
                .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsSolarizedLightSelected)),
                
                new RadioButton 
                { 
                    GroupName = "ThemeGroup", 
                    Content = "Sepia", 
                    BackgroundColor = Colors.Transparent
                }
                .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsSepiaSelected)),

                new Label 
                { 
                    Text = "Accessibility Themes",
                    FontAttributes = FontAttributes.Bold,
                    Margin = new(0, 15, 0, 5)
                },
                
                new RadioButton 
                { 
                    GroupName = "ThemeGroup",
                    Content = "High Contrast Dark",
                    BackgroundColor = Colors.Transparent 
                }
                .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsHighContrastDarkSelected)),
                
                new RadioButton
                { 
                    GroupName = "ThemeGroup",
                    Content = "High Contrast Light",
                    BackgroundColor = Colors.Transparent
                }
                .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsHighContrastLightSelected)),
            }
        };

        mainLayout.Children.Add(new BoxView
        { 
            HeightRequest = 1,
            BackgroundColor = Color.FromArgb("#333333"),
            Margin = new(0, 15, 0, 15)
        });
        
        mainLayout.Children.Add(new Label
        {
            Text = "Calendar Settings",
            FontSize = LargeFontSize,
            FontAttributes = FontAttributes.Bold,
            Margin = new(0, 0, 0, 10)
        });
        
        mainLayout.Children.Add(new Label
        { 
            Text = "Choose the calendar system for displaying dates:",
            Style = GetStyle("LightGrayLabel"),
            Margin = new(0, 0, 0, 5)
        });
        
        mainLayout.Children.Add(new RadioButton
        {
            GroupName = "CalendarGroup",
            Content = "Gregorian Calendar",
            BackgroundColor = Colors.Transparent
        }
        .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsGregorianSelected)));
        
        mainLayout.Children.Add(new RadioButton
        {
            GroupName = "CalendarGroup",
            Content = "Persian (Shamsi) Calendar",
            BackgroundColor = Colors.Transparent
        }
        .Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsPersianSelected)));

        mainLayout.Children.Add(new BoxView 
        { 
            HeightRequest = 1, 
            BackgroundColor = Color.FromArgb("#333333"), 
            Margin = new(0, 15, 0, 15)
        });
        
        mainLayout.Children.Add(new Label 
        { 
            Text = "Data Management", 
            FontSize = LargeFontSize, 
            FontAttributes = FontAttributes.Bold, 
            Margin = new(0, 0, 0, 10) 
        });
        
        mainLayout.Children.Add(new Label 
        { 
            Text = "Export/Import all app data (tasks, settings, progress). Importing replaces current data.",
            Style = GetStyle("LightGrayLabel"),
            LineBreakMode = LineBreakMode.WordWrap,
            Margin = new(0, 0, 0, 10) 
        });
        
        mainLayout.Children.Add(new HorizontalStackLayout
        {
            Spacing = 10,
            Margin = new(0, 10, 0, 0),
            Children =
            {
                new Button 
                { 
                    Text = "Export Data",
                    BackgroundColor = Color.FromArgb("#4A6FA5"),
                    TextColor = Colors.WhiteSmoke 
                }
                .BindCommand(nameof(SettingsViewModel.ExportDataCommand)),
                
                new Button 
                { 
                    Text = "Import Data", 
                    BackgroundColor = Color.FromArgb("#5A9A78"), 
                    TextColor = Colors.WhiteSmoke 
                }
                .BindCommand(nameof(SettingsViewModel.ImportDataCommand))
            }
        });

        Content = new ScrollView 
        { 
            Content = mainLayout 
        };
    }

    private static Style GetStyle(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var resource) == true && resource is Style style)
        {
            return style;
        }

        if (Application.Current?.Resources.TryGetValue("BaseLabelStyle", out var baseResource) == true && baseResource is Style baseStyle)
        {
            return baseStyle;
        }

        return new(typeof(Label));
    }
}