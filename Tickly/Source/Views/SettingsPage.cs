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
        // REMOVED: Explicit background binding - rely on Style targeting Page
        // this.SetBinding(BackgroundColorProperty, new Binding("AppBackgroundColor", source: Application.Current!.Resources));

        var mainLayout = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 15,
            Children =
            {
                new Label { Text = "Theme Settings", FontSize = LargeFontSize, FontAttributes = FontAttributes.Bold, Margin = new(0, 0, 0, 10) },
                new Label { Text = "Choose the application theme:", Style = GetStyle("LightGrayLabel"), Margin = new(0,0,0,5) },

                // Theme RadioButtons... (Keep existing buttons)
                new Label { Text = "Dark Themes", FontAttributes = FontAttributes.Bold, Margin = new(0, 10, 0, 5) },
                new RadioButton { GroupName = "ThemeGroup", Content = "Pitch Black" }.Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsPitchBlackSelected)),
                new RadioButton { GroupName = "ThemeGroup", Content = "Dark Gray" }.Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsDarkGraySelected)),
                new RadioButton { GroupName = "ThemeGroup", Content = "Nord" }.Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsNordSelected)),
                new RadioButton { GroupName = "ThemeGroup", Content = "Catppuccin Mocha" }.Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsCatppuccinMochaSelected)),
                new RadioButton { GroupName = "ThemeGroup", Content = "Solarized Dark" }.Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsSolarizedDarkSelected)),
                new RadioButton { GroupName = "ThemeGroup", Content = "Gruvbox Dark" }.Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsGruvboxDarkSelected)),
                new RadioButton { GroupName = "ThemeGroup", Content = "Monokai" }.Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsMonokaiSelected)),

                new Label { Text = "Light Themes", FontAttributes = FontAttributes.Bold, Margin = new(0, 15, 0, 5) },
                new RadioButton { GroupName = "ThemeGroup", Content = "Default Light" }.Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsLightSelected)),
                new RadioButton { GroupName = "ThemeGroup", Content = "Solarized Light" }.Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsSolarizedLightSelected)),
                new RadioButton { GroupName = "ThemeGroup", Content = "Sepia" }.Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsSepiaSelected)),

                new Label { Text = "Accessibility Themes", FontAttributes = FontAttributes.Bold, Margin = new(0, 15, 0, 5) },
                new RadioButton { GroupName = "ThemeGroup", Content = "High Contrast Dark" }.Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsHighContrastDarkSelected)),
                new RadioButton { GroupName = "ThemeGroup", Content = "High Contrast Light" }.Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsHighContrastLightSelected)),
            }
        };

#if WINDOWS
        mainLayout.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#333333"), Margin = new(0, 15, 0, 15) });
        mainLayout.Children.Add(new Label { Text = "Windows Settings", FontSize = LargeFontSize, FontAttributes = FontAttributes.Bold, Margin = new(0, 0, 0, 10) });
        mainLayout.Children.Add(new HorizontalStackLayout
        {
             Spacing = 10, VerticalOptions = LayoutOptions.Center,
             Children =
             {
                 new Label { Text = "Use System Background (Mica/Acrylic)", Style = GetStyle("BaseLabelStyle"), VerticalOptions = LayoutOptions.Center },
                 new Switch { VerticalOptions = LayoutOptions.Center }.Bind(Switch.IsToggledProperty, nameof(SettingsViewModel.UseSystemBackground))
             }
        });
        mainLayout.Children.Add(new Label { Text = "Applies transparency effect. May require restart.", Style = GetStyle("LightGrayLabel"), FontSize = SmallFontSize });
#endif

        mainLayout.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#333333"), Margin = new(0, 15, 0, 15) });
        mainLayout.Children.Add(new Label { Text = "Calendar Settings", FontSize = LargeFontSize, FontAttributes = FontAttributes.Bold, Margin = new(0, 0, 0, 10) });
        mainLayout.Children.Add(new Label { Text = "Choose the calendar system for displaying dates:", Style = GetStyle("LightGrayLabel"), Margin = new(0, 0, 0, 5) });
        mainLayout.Children.Add(new RadioButton { GroupName = "CalendarGroup", Content = "Gregorian Calendar" }.Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsGregorianSelected)));
        mainLayout.Children.Add(new RadioButton { GroupName = "CalendarGroup", Content = "Persian (Shamsi) Calendar" }.Bind(RadioButton.IsCheckedProperty, nameof(SettingsViewModel.IsPersianSelected)));

        mainLayout.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#333333"), Margin = new(0, 15, 0, 15) });
        mainLayout.Children.Add(new Label { Text = "Data Management", FontSize = LargeFontSize, FontAttributes = FontAttributes.Bold, Margin = new(0, 0, 0, 10) });
        mainLayout.Children.Add(new Label { Text = "Export/Import all app data (tasks, settings, progress). Importing replaces current data.", Style = GetStyle("LightGrayLabel"), LineBreakMode = LineBreakMode.WordWrap, Margin = new(0, 0, 0, 10) });
        mainLayout.Children.Add(new HorizontalStackLayout
        {
            Spacing = 10,
            Margin = new(0, 10, 0, 0),
            Children =
            {
                new Button { Text = "Export Data", BackgroundColor = Color.FromArgb("#4A6FA5"), TextColor = Colors.WhiteSmoke }.BindCommand(nameof(SettingsViewModel.ExportDataCommand)),
                new Button { Text = "Import Data", BackgroundColor = Color.FromArgb("#5A9A78"), TextColor = Colors.WhiteSmoke }.BindCommand(nameof(SettingsViewModel.ImportDataCommand))
            }
        });

        Content = new ScrollView { Content = mainLayout };
    }

    private static Style GetStyle(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var resource) == true && resource is Style style) return style;
        if (Application.Current?.Resources.TryGetValue("BaseLabelStyle", out var baseResource) == true && baseResource is Style baseStyle) return baseStyle;
        return new Style(typeof(Label));
    }
}