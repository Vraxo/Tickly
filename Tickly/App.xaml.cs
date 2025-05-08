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
    private const int WindowWidth = 450;
    private const int WindowHeight = 800;
    private readonly ThemeService _themeService; // Added

    public App(ThemeService themeService) // Inject service
    {
        InitializeComponent();
        _themeService = themeService; // Store injected service

        // Apply initial theme using the service
        _themeService.ApplyTheme(AppSettings.SelectedTheme);

        MainPage = new AppShell();

        // Register message handler to use the service
        WeakReferenceMessenger.Default.Register<ThemeChangedMessage>(this, (r, m) => _themeService.ApplyTheme(m.Value));
    }

    // REMOVED: ApplyTheme method (logic moved to ThemeService)

    protected override void OnStart()
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
            // Use GetService safely
            var mainViewModel = IPlatformApplication.Current?.Services?.GetService<MainViewModel>();
            mainViewModel?.FinalizeAndSaveProgressAsync().FireAndForgetSafeAsync();
            Debug.WriteLine("App.OnSleep: FinalizeAndSaveProgressAsync requested.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"App.OnSleep: Error during saving progress: {ex.Message}");
        }
    }

    protected override void OnResume()
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
                    IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                    if (hwnd == IntPtr.Zero) return;

                    Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                    if (windowId.Value == 0) return;

                    Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                    if (appWindow != null)
                    {
                         Microsoft.UI.Windowing.DisplayArea displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
                        if (displayArea != null)
                        {
                            Windows.Graphics.RectInt32 mainMonitorWorkArea = displayArea.WorkArea;
                            int centeredX = (mainMonitorWorkArea.Width - WindowWidth) / 2;
                            int centeredY = (mainMonitorWorkArea.Height - WindowHeight) / 2;

                            appWindow.MoveAndResize(new Windows.Graphics.RectInt32(centeredX, centeredY, WindowWidth, WindowHeight));
                            Debug.WriteLine($"App.CreateWindow (Windows): Centered and resized window to {WindowWidth}x{WindowHeight}.");
                        }
                        else
                        {
                            appWindow.ResizeClient(new Windows.Graphics.SizeInt32(WindowWidth, WindowHeight));
                            Debug.WriteLine($"App.CreateWindow (Windows): Resized client area to {WindowWidth}x{WindowHeight} (DisplayArea not found).");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"App.CreateWindow (Windows): Error resizing/centering window: {ex.Message}");
                }
            }
        };
#endif

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
            Debug.WriteLine($"FireAndForgetSafeAsync: Error in awaited task: {ex}");
            errorHandler?.Invoke(ex);
        }
    }
}