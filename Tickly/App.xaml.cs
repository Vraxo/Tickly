using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.LifecycleEvents;
using Tickly.ViewModels;
using Tickly.Services;
using System.Diagnostics;
using Tickly.Messages;
using Tickly.Models;

namespace Tickly;

public partial class App : Application
{
    const int WindowWidth = 450;
    const int WindowHeight = 800;

    public App()
    {
        InitializeComponent();
        ApplyTheme(AppSettings.SelectedTheme);
        MainPage = new AppShell();
        WeakReferenceMessenger.Default.Register<ThemeChangedMessage>(this, (r, m) => ApplyTheme(m.Value));
    }

    private void ApplyTheme(ThemeType theme)
    {
        if (Application.Current == null) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            string backgroundKey;
            string surfaceKey;
            string foregroundKey;
            string secondaryTextKey;
            string primaryActionBgKey;
            string primaryActionFgKey;

            switch (theme)
            {
                case ThemeType.Light:
                    backgroundKey = "LightBackgroundColor";
                    surfaceKey = "LightSurfaceColor";
                    foregroundKey = "LightForegroundColor";
                    secondaryTextKey = "LightSecondaryTextColor";
                    primaryActionBgKey = "LightPrimaryActionBackgroundColor";
                    primaryActionFgKey = "LightPrimaryActionForegroundColor";
                    Application.Current.UserAppTheme = AppTheme.Light;
                    break;
                case ThemeType.DarkGray:
                    backgroundKey = "DarkGrayBackgroundColor";
                    surfaceKey = "DarkGraySurfaceColor";
                    foregroundKey = "DarkGrayForegroundColor";
                    secondaryTextKey = "DarkGraySecondaryTextColor";
                    primaryActionBgKey = "DarkGrayPrimaryActionBackgroundColor"; // Use specific Dark Gray key
                    primaryActionFgKey = "DarkGrayPrimaryActionForegroundColor"; // Use specific Dark Gray key
                    Application.Current.UserAppTheme = AppTheme.Dark;
                    break;
                case ThemeType.Nord:
                    backgroundKey = "NordBackgroundColor";
                    surfaceKey = "NordSurfaceColor";
                    foregroundKey = "NordForegroundColor";
                    secondaryTextKey = "NordSecondaryTextColor";
                    primaryActionBgKey = "NordPrimaryActionBackgroundColor"; // Use specific Nord key
                    primaryActionFgKey = "NordPrimaryActionForegroundColor"; // Use specific Nord key
                    Application.Current.UserAppTheme = AppTheme.Dark;
                    break;
                case ThemeType.PitchBlack:
                default:
                    backgroundKey = "PitchBlackBackgroundColor";
                    surfaceKey = "PitchBlackSurfaceColor";
                    foregroundKey = "PitchBlackForegroundColor";
                    secondaryTextKey = "PitchBlackSecondaryTextColor";
                    primaryActionBgKey = "PitchBlackPrimaryActionBackgroundColor"; // Use specific Pitch Black key
                    primaryActionFgKey = "PitchBlackPrimaryActionForegroundColor"; // Use specific Pitch Black key
                    Application.Current.UserAppTheme = AppTheme.Dark;
                    break;
            }

            Application.Current.Resources["AppBackgroundColor"] = Application.Current.Resources[backgroundKey];
            Application.Current.Resources["AppSurfaceColor"] = Application.Current.Resources[surfaceKey];
            Application.Current.Resources["AppForegroundColor"] = Application.Current.Resources[foregroundKey];
            Application.Current.Resources["AppSecondaryTextColor"] = Application.Current.Resources[secondaryTextKey];
            Application.Current.Resources["AppPrimaryActionBackgroundColor"] = Application.Current.Resources[primaryActionBgKey];
            Application.Current.Resources["AppPrimaryActionForegroundColor"] = Application.Current.Resources[primaryActionFgKey];

            Debug.WriteLine($"Theme applied: {theme}. MAUI Theme: {Application.Current.UserAppTheme}. Background: {Application.Current.Resources["AppBackgroundColor"]}, Surface: {Application.Current.Resources["AppSurfaceColor"]}, Foreground: {Application.Current.Resources["AppForegroundColor"]}, Secondary: {Application.Current.Resources["AppSecondaryTextColor"]}, ActionBG: {Application.Current.Resources["AppPrimaryActionBackgroundColor"]}, ActionFG: {Application.Current.Resources["AppPrimaryActionForegroundColor"]}");

            if (MainPage is Shell shell)
            {
                shell.BackgroundColor = Colors.Transparent;
                shell.BackgroundColor = (Color)Application.Current.Resources["AppBackgroundColor"];
            }
        });
    }

    protected override async void OnStart()
    {
        base.OnStart();
        Debug.WriteLine("App.OnStart: Application started.");
    }

    protected override async void OnSleep()
    {
        base.OnSleep();
        Debug.WriteLine("App.OnSleep: Application is going to sleep. Saving final progress.");
        try
        {
            var mainViewModel = IPlatformApplication.Current?.Services?.GetService<MainViewModel>();
            mainViewModel?.FinalizeAndSaveProgressAsync().FireAndForgetSafeAsync();
            Debug.WriteLine("App.OnSleep: FinalizeAndSaveProgressAsync completed or task running.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"App.OnSleep: Error during saving progress: {ex.Message}");
        }
    }

    protected override async void OnResume()
    {
        base.OnResume();
        Debug.WriteLine("App.OnResume: Application is resuming.");
    }

    protected override Window CreateWindow(IActivationState activationState)
    {
        Window window = base.CreateWindow(activationState);

#if WINDOWS
        window.Created += (s, e) =>
        {
            var mauiWindow = s as Window;
            if (mauiWindow == null) return;

            var handler = mauiWindow.Handler;
            if (handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
            {
                try
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                    if (hwnd == IntPtr.Zero) return;

                    Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                    if (windowId.Value == 0) return;

                    Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                    if (appWindow != null)
                    {
                         Microsoft.UI.Windowing.DisplayArea displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
                        if (displayArea != null)
                        {
                            var mainMonitorWorkArea = displayArea.WorkArea;
                            var centeredX = (mainMonitorWorkArea.Width - WindowWidth) / 2;
                            var centeredY = (mainMonitorWorkArea.Height - WindowHeight) / 2;

                            appWindow.MoveAndResize(new Windows.Graphics.RectInt32(centeredX, centeredY, WindowWidth, WindowHeight));
                        }
                        else
                        {
                            appWindow.ResizeClient(new Windows.Graphics.SizeInt32(WindowWidth, WindowHeight));
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error resizing window: {ex.Message}");
                }
            }
        };
#endif

        return window;
    }
}

// Simple Fire and Forget helper
internal static class TaskExtensions
{
    internal static async void FireAndForgetSafeAsync(this Task task,
        Action<Exception>? errorHandler = null)
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