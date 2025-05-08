using System.Collections.Generic;
using System.Diagnostics;
using Tickly.Models;

namespace Tickly.Services;

public sealed class ThemeService
{
    private const string AppBackgroundColorKey = "AppBackgroundColor";
    private const string AppSurfaceColorKey = "AppSurfaceColor";
    private const string AppForegroundColorKey = "AppForegroundColor";
    private const string AppSecondaryTextColorKey = "AppSecondaryTextColor";
    private const string AppPrimaryActionBackgroundColorKey = "AppPrimaryActionBackgroundColor";
    private const string AppPrimaryActionForegroundColorKey = "AppPrimaryActionForegroundColor";

    private readonly record struct ThemeResourceKeys(
        string Background,
        string Surface,
        string Foreground,
        string SecondaryText,
        string PrimaryActionBg,
        string PrimaryActionFg,
        AppTheme MauiTheme
    );

    private readonly Dictionary<ThemeType, ThemeResourceKeys> _themeKeysMap = new()
    {
        // Dark Themes
        [ThemeType.PitchBlack] = new ThemeResourceKeys(
            Background: "PitchBlackBackgroundColor", Surface: "PitchBlackSurfaceColor", Foreground: "PitchBlackForegroundColor",
            SecondaryText: "PitchBlackSecondaryTextColor", PrimaryActionBg: "PitchBlackPrimaryActionBackgroundColor", PrimaryActionFg: "PitchBlackPrimaryActionForegroundColor", MauiTheme: AppTheme.Dark
        ),
        [ThemeType.DarkGray] = new ThemeResourceKeys(
            Background: "DarkGrayBackgroundColor", Surface: "DarkGraySurfaceColor", Foreground: "DarkGrayForegroundColor",
            SecondaryText: "DarkGraySecondaryTextColor", PrimaryActionBg: "DarkGrayPrimaryActionBackgroundColor", PrimaryActionFg: "DarkGrayPrimaryActionForegroundColor", MauiTheme: AppTheme.Dark
        ),
        [ThemeType.Nord] = new ThemeResourceKeys(
            Background: "NordBackgroundColor", Surface: "NordSurfaceColor", Foreground: "NordForegroundColor",
            SecondaryText: "NordSecondaryTextColor", PrimaryActionBg: "NordPrimaryActionBackgroundColor", PrimaryActionFg: "NordPrimaryActionForegroundColor", MauiTheme: AppTheme.Dark
        ),
        [ThemeType.CatppuccinMocha] = new ThemeResourceKeys(
            Background: "CatppuccinMochaBackgroundColor", Surface: "CatppuccinMochaSurfaceColor", Foreground: "CatppuccinMochaForegroundColor",
            SecondaryText: "CatppuccinMochaSecondaryTextColor", PrimaryActionBg: "CatppuccinMochaPrimaryActionBackgroundColor", PrimaryActionFg: "CatppuccinMochaPrimaryActionForegroundColor", MauiTheme: AppTheme.Dark
        ),
        [ThemeType.SolarizedDark] = new ThemeResourceKeys(
            Background: "SolarizedDarkBackgroundColor", Surface: "SolarizedDarkSurfaceColor", Foreground: "SolarizedDarkForegroundColor",
            SecondaryText: "SolarizedDarkSecondaryTextColor", PrimaryActionBg: "SolarizedDarkPrimaryActionBackgroundColor", PrimaryActionFg: "SolarizedDarkPrimaryActionForegroundColor", MauiTheme: AppTheme.Dark
        ),
        [ThemeType.GruvboxDark] = new ThemeResourceKeys(
            Background: "GruvboxDarkBackgroundColor", Surface: "GruvboxDarkSurfaceColor", Foreground: "GruvboxDarkForegroundColor",
            SecondaryText: "GruvboxDarkSecondaryTextColor", PrimaryActionBg: "GruvboxDarkPrimaryActionBackgroundColor", PrimaryActionFg: "GruvboxDarkPrimaryActionForegroundColor", MauiTheme: AppTheme.Dark
        ),
        [ThemeType.Monokai] = new ThemeResourceKeys(
            Background: "MonokaiBackgroundColor", Surface: "MonokaiSurfaceColor", Foreground: "MonokaiForegroundColor",
            SecondaryText: "MonokaiSecondaryTextColor", PrimaryActionBg: "MonokaiPrimaryActionBackgroundColor", PrimaryActionFg: "MonokaiPrimaryActionForegroundColor", MauiTheme: AppTheme.Dark
        ),
        [ThemeType.HighContrastDark] = new ThemeResourceKeys(
            Background: "HighContrastDarkBackgroundColor", Surface: "HighContrastDarkSurfaceColor", Foreground: "HighContrastDarkForegroundColor",
            SecondaryText: "HighContrastDarkSecondary", PrimaryActionBg: "HighContrastDarkActionBg", PrimaryActionFg: "HighContrastDarkActionFg", MauiTheme: AppTheme.Dark
        ),
        // Light Themes
        [ThemeType.Light] = new ThemeResourceKeys(
            Background: "LightBackgroundColor", Surface: "LightSurfaceColor", Foreground: "LightForegroundColor",
            SecondaryText: "LightSecondaryTextColor", PrimaryActionBg: "LightPrimaryActionBackgroundColor", PrimaryActionFg: "LightPrimaryActionForegroundColor", MauiTheme: AppTheme.Light
        ),
        [ThemeType.SolarizedLight] = new ThemeResourceKeys(
            Background: "SolarizedLightBackgroundColor", Surface: "SolarizedLightSurfaceColor", Foreground: "SolarizedLightForegroundColor",
            SecondaryText: "SolarizedLightSecondaryTextColor", PrimaryActionBg: "SolarizedLightPrimaryActionBackgroundColor", PrimaryActionFg: "SolarizedLightPrimaryActionForegroundColor", MauiTheme: AppTheme.Light
        ),
        [ThemeType.Sepia] = new ThemeResourceKeys(
            Background: "SepiaBackgroundColor", Surface: "SepiaSurfaceColor", Foreground: "SepiaForegroundColor",
            SecondaryText: "SepiaSecondaryTextColor", PrimaryActionBg: "SepiaPrimaryActionBackgroundColor", PrimaryActionFg: "SepiaPrimaryActionForegroundColor", MauiTheme: AppTheme.Light
        ),
        [ThemeType.HighContrastLight] = new ThemeResourceKeys(
            Background: "HighContrastLightBackgroundColor", Surface: "HighContrastLightSurfaceColor", Foreground: "HighContrastLightForegroundColor",
            SecondaryText: "HighContrastLightSecondary", PrimaryActionBg: "HighContrastLightActionBg", PrimaryActionFg: "HighContrastLightActionFg", MauiTheme: AppTheme.Light
        )
    };

    public void ApplyTheme(ThemeType theme)
    {
        if (Application.Current == null)
        {
            Debug.WriteLine("ThemeService.ApplyTheme: Application.Current is null. Cannot apply theme.");
            return;
        }

        if (!_themeKeysMap.TryGetValue(theme, out ThemeResourceKeys keys))
        {
            Debug.WriteLine($"ThemeService.ApplyTheme: ThemeType '{theme}' not found in map. Defaulting to PitchBlack.");
            keys = _themeKeysMap[ThemeType.PitchBlack];
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var resources = Application.Current.Resources;

                resources[AppBackgroundColorKey] = resources[keys.Background];
                resources[AppSurfaceColorKey] = resources[keys.Surface];
                resources[AppForegroundColorKey] = resources[keys.Foreground];
                resources[AppSecondaryTextColorKey] = resources[keys.SecondaryText];
                resources[AppPrimaryActionBackgroundColorKey] = resources[keys.PrimaryActionBg];
                resources[AppPrimaryActionForegroundColorKey] = resources[keys.PrimaryActionFg];

                Application.Current.UserAppTheme = keys.MauiTheme;

                Debug.WriteLine($"ThemeService: Theme applied: {theme}. MAUI Theme: {keys.MauiTheme}.");
            }
            catch (KeyNotFoundException knfEx)
            {
                Debug.WriteLine($"ThemeService.ApplyTheme: Error - Resource key not found: {knfEx.Message}. Theme application incomplete.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ThemeService.ApplyTheme: Error applying theme resources: {ex.GetType().Name} - {ex.Message}");
            }
        });
    }
}