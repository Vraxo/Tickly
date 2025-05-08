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
        string Background, string Surface, string Foreground, string SecondaryText,
        string PrimaryActionBg, string PrimaryActionFg, AppTheme MauiTheme
    );

    private readonly Dictionary<ThemeType, ThemeResourceKeys> _themeKeysMap = new()
    {
        // Dark Themes
        [ThemeType.PitchBlack] = new("PitchBlackBackgroundColor", "PitchBlackSurfaceColor", "PitchBlackForegroundColor", "PitchBlackSecondaryTextColor", "PitchBlackPrimaryActionBackgroundColor", "PitchBlackPrimaryActionForegroundColor", AppTheme.Dark),
        [ThemeType.DarkGray] = new("DarkGrayBackgroundColor", "DarkGraySurfaceColor", "DarkGrayForegroundColor", "DarkGraySecondaryTextColor", "DarkGrayPrimaryActionBackgroundColor", "DarkGrayPrimaryActionForegroundColor", AppTheme.Dark),
        [ThemeType.Nord] = new("NordBackgroundColor", "NordSurfaceColor", "NordForegroundColor", "NordSecondaryTextColor", "NordPrimaryActionBackgroundColor", "NordPrimaryActionForegroundColor", AppTheme.Dark),
        [ThemeType.CatppuccinMocha] = new("CatppuccinMochaBackgroundColor", "CatppuccinMochaSurfaceColor", "CatppuccinMochaForegroundColor", "CatppuccinMochaSecondaryTextColor", "CatppuccinMochaPrimaryActionBackgroundColor", "CatppuccinMochaPrimaryActionForegroundColor", AppTheme.Dark),
        [ThemeType.SolarizedDark] = new("SolarizedDarkBackgroundColor", "SolarizedDarkSurfaceColor", "SolarizedDarkForegroundColor", "SolarizedDarkSecondaryTextColor", "SolarizedDarkPrimaryActionBackgroundColor", "SolarizedDarkPrimaryActionForegroundColor", AppTheme.Dark),
        [ThemeType.GruvboxDark] = new("GruvboxDarkBackgroundColor", "GruvboxDarkSurfaceColor", "GruvboxDarkForegroundColor", "GruvboxDarkSecondaryTextColor", "GruvboxDarkPrimaryActionBackgroundColor", "GruvboxDarkPrimaryActionForegroundColor", AppTheme.Dark),
        [ThemeType.Monokai] = new("MonokaiBackgroundColor", "MonokaiSurfaceColor", "MonokaiForegroundColor", "MonokaiSecondaryTextColor", "MonokaiPrimaryActionBackgroundColor", "MonokaiPrimaryActionForegroundColor", AppTheme.Dark),
        [ThemeType.HighContrastDark] = new("HighContrastDarkBackgroundColor", "HighContrastDarkSurfaceColor", "HighContrastDarkForegroundColor", "HighContrastDarkSecondary", "HighContrastDarkActionBg", "HighContrastDarkActionFg", AppTheme.Dark),
        // Light Themes
        [ThemeType.Light] = new("LightBackgroundColor", "LightSurfaceColor", "LightForegroundColor", "LightSecondaryTextColor", "LightPrimaryActionBackgroundColor", "LightPrimaryActionForegroundColor", AppTheme.Light),
        [ThemeType.SolarizedLight] = new("SolarizedLightBackgroundColor", "SolarizedLightSurfaceColor", "SolarizedLightForegroundColor", "SolarizedLightSecondaryTextColor", "SolarizedLightPrimaryActionBackgroundColor", "SolarizedLightPrimaryActionForegroundColor", AppTheme.Light),
        [ThemeType.Sepia] = new("SepiaBackgroundColor", "SepiaSurfaceColor", "SepiaForegroundColor", "SepiaSecondaryTextColor", "SepiaPrimaryActionBackgroundColor", "SepiaPrimaryActionForegroundColor", AppTheme.Light),
        [ThemeType.HighContrastLight] = new("HighContrastLightBackgroundColor", "HighContrastLightSurfaceColor", "HighContrastLightForegroundColor", "HighContrastLightSecondary", "HighContrastLightActionBg", "HighContrastLightActionFg", AppTheme.Light)
    };

    public void ApplyTheme(ThemeType theme)
    {
        var currentApp = Microsoft.Maui.Controls.Application.Current;
        if (currentApp == null)
        {
            Debug.WriteLine("ThemeService.ApplyTheme: Application.Current is null.");
            return;
        }

        if (!_themeKeysMap.TryGetValue(theme, out ThemeResourceKeys keys))
        {
            Debug.WriteLine($"ThemeService.ApplyTheme: ThemeType '{theme}' not found. Defaulting to PitchBlack.");
            keys = _themeKeysMap[ThemeType.PitchBlack];
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var resources = currentApp.Resources;

                // Set the *actual* theme colors in the resources
                resources[AppBackgroundColorKey] = resources[keys.Background];
                resources[AppSurfaceColorKey] = resources[keys.Surface];
                resources[AppForegroundColorKey] = resources[keys.Foreground];
                resources[AppSecondaryTextColorKey] = resources[keys.SecondaryText];
                resources[AppPrimaryActionBackgroundColorKey] = resources[keys.PrimaryActionBg];
                resources[AppPrimaryActionForegroundColorKey] = resources[keys.PrimaryActionFg];

                currentApp.UserAppTheme = keys.MauiTheme;

                Debug.WriteLine($"ThemeService: Applied theme resource colors for: {theme}. MAUI Theme: {keys.MauiTheme}.");
            }
            catch (KeyNotFoundException knfEx)
            {
                Debug.WriteLine($"ThemeService.ApplyTheme: Error - Resource key not found: {knfEx.Message}.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ThemeService.ApplyTheme: Error applying theme resources: {ex.GetType().Name} - {ex.Message}");
            }
        });
    }
}