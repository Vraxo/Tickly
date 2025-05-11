using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.LifecycleEvents;
using Tickly.ViewModels;
using Tickly.Services;
using Tickly.Messages;
using Tickly.Models;

#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;
#endif

namespace Tickly;

public partial class App : Microsoft.Maui.Controls.Application
{
    private const int WindowWidth = 450;
    private const int WindowHeight = 800;

    private readonly ThemeService _themeService;

#if WINDOWS
    private Microsoft.UI.Xaml.Window? _nativeWindow;
#endif

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

    protected override void OnStart()
    {
        base.OnStart();
    }

    protected override async void OnSleep()
    {
        base.OnSleep();
        try
        {
            IPlatformApplication.Current?.Services?.GetService<MainViewModel>()?.FinalizeAndSaveProgressAsync().FireAndForgetSafeAsync();
        }
        catch (Exception)
        {
            // Error already logged by FireAndForgetSafeAsync
        }
    }

    protected override void OnResume()
    {
        base.OnResume();
    }

    protected override Microsoft.Maui.Controls.Window CreateWindow(IActivationState activationState)
    {
        Microsoft.Maui.Controls.Window window = base.CreateWindow(activationState);
        window.Stopped += Window_Stopped;

#if WINDOWS
        window.Created += Window_Created_Windows;
#endif
        return window;
    }

    private void Window_Stopped(object? sender, EventArgs e)
    {
#if WINDOWS
         _nativeWindow = null;
#endif
    }

#if WINDOWS
    private void Window_Created_Windows(object? sender, EventArgs e)
    {
        var mauiWindow = sender as Microsoft.Maui.Controls.Window;
        if (mauiWindow == null)
        {
            return;
        }

        _nativeWindow = mauiWindow.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        if (_nativeWindow != null)
        {
            try
            {
                PositionAndResizeWindow();
            }
            catch (Exception)
            {
                // Error already logged by PositionAndResizeWindow if it occurs
            }
        }
    }

    private void PositionAndResizeWindow()
    {
        if (_nativeWindow == null)
        {
            return;
        }

        IntPtr hwnd = WindowNative.GetWindowHandle(_nativeWindow);
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        if (windowId.Value == 0)
        {
            return;
        }
        
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        if (appWindow != null)
        {
            DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
            if (displayArea != null)
            {
                Windows.Graphics.RectInt32 workArea = displayArea.WorkArea;
                int x = (workArea.Width - WindowWidth) / 2;
                int y = (workArea.Height - WindowHeight) / 2;
                appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, WindowWidth, WindowHeight));
            }
            else
            {
                appWindow.ResizeClient(new Windows.Graphics.SizeInt32(WindowWidth, WindowHeight));
            }
        }
    }
#endif
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
            errorHandler?.Invoke(ex);
        }
    }
}