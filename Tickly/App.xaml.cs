using CommunityToolkit.Mvvm.Messaging;
using Tickly.ViewModels;
using Tickly.Services;
using System.Diagnostics;
using Tickly.Messages;

namespace Tickly;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly ThemeService _themeService;

    public App(ThemeService themeService)
    {
        InitializeComponent();
        _themeService = themeService;

        _themeService.ApplyTheme(AppSettings.SelectedTheme);

        MainPage = new AppShell();

        WeakReferenceMessenger.Default.Register<ThemeChangedMessage>(this, HandleThemeChanged);
    }

    private void HandleThemeChanged(object recipient, ThemeChangedMessage message)
    {
        _themeService.ApplyTheme(message.Value);
    }

    private void ApplyBackgroundsBasedOnSetting(bool useSystemBackground)
    {
    }

    protected override void OnStart()
    {
        base.OnStart();
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        try
        {
            IPlatformApplication.Current?.Services?.GetService<MainViewModel>()?.FinalizeAndSaveProgressAsync().FireAndForgetSafeAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"App.OnSleep: Error: {ex.Message}");
        }
    }

    protected override void OnResume()
    {
        base.OnResume();
    }

    protected override Microsoft.Maui.Controls.Window CreateWindow(IActivationState activationState)
    {
        Microsoft.Maui.Controls.Window window = base.CreateWindow(activationState);
        return window;
    }
}

internal static class TaskExtensions
{
    internal static async void FireAndForgetSafeAsync(this Task task, Action<Exception>? errorHandler = null)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FireAndForgetSafeAsync: Error: {ex}");
            errorHandler?.Invoke(ex);
        }
    }
}