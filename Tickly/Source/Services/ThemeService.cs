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
        [ThemeType.PitchBlack] = new ThemeResourceKeys(
            Background: "PitchBlackBackgroundColor",
            Surface: "PitchBlackSurfaceColor",
            Foreground: "PitchBlackForegroundColor",
            SecondaryText: "PitchBlackSecondaryTextColor",
            PrimaryActionBg: "PitchBlackPrimaryActionBackgroundColor",
            PrimaryActionFg: "PitchBlackPrimaryActionForegroundColor",
            MauiTheme: AppTheme.Dark
        ),
        [ThemeType.DarkGray] = new ThemeResourceKeys(
            Background: "DarkGrayBackgroundColor",
            Surface: "DarkGraySurfaceColor",
            Foreground: "DarkGrayForegroundColor",
            SecondaryText: "DarkGraySecondaryTextColor",
            PrimaryActionBg: "DarkGrayPrimaryActionBackgroundColor",
            PrimaryActionFg: "DarkGrayPrimaryActionForegroundColor",
            MauiTheme: AppTheme.Dark
        ),
        [ThemeType.Nord] = new ThemeResourceKeys(
            Background: "NordBackgroundColor",
            Surface: "NordSurfaceColor",
            Foreground: "NordForegroundColor",
            SecondaryText: "NordSecondaryTextColor",
            PrimaryActionBg: "NordPrimaryActionBackgroundColor",
            PrimaryActionFg: "NordPrimaryActionForegroundColor",
            MauiTheme: AppTheme.Dark
        ),
        [ThemeType.Light] = new ThemeResourceKeys(
            Background: "LightBackgroundColor",
            Surface: "LightSurfaceColor",
            Foreground: "LightForegroundColor",
            SecondaryText: "LightSecondaryTextColor",
            PrimaryActionBg: "LightPrimaryActionBackgroundColor",
            PrimaryActionFg: "LightPrimaryActionForegroundColor",
            MauiTheme: AppTheme.Light
        ),
        // Added Catppuccin Mocha
        [ThemeType.CatppuccinMocha] = new ThemeResourceKeys(
            Background: "CatppuccinMochaBackgroundColor",
            Surface: "CatppuccinMochaSurfaceColor",
            Foreground: "CatppuccinMochaForegroundColor",
            SecondaryText: "CatppuccinMochaSecondaryTextColor",
            PrimaryActionBg: "CatppuccinMochaPrimaryActionBackgroundColor",
            PrimaryActionFg: "CatppuccinMochaPrimaryActionForegroundColor",
            MauiTheme: AppTheme.Dark // It's a dark theme
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
            keys = _themeKeysMap[ThemeType.PitchBlack]; // Fallback safely
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