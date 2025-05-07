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
        ApplyTheme(AppSettings.SelectedTheme); // Apply theme on startup
        MainPage = new AppShell();
        WeakReferenceMessenger.Default.Register<ThemeChangedMessage>(this, (r, m) => ApplyTheme(m.Value));
    }

    private void ApplyTheme(ThemeType theme)
    {
        if (Application.Current == null) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            string backgroundKey = theme == ThemeType.DarkGray ? "DarkGrayBackgroundColor" : "PitchBlackBackgroundColor";
            string surfaceKey = theme == ThemeType.DarkGray ? "DarkGraySurfaceColor" : "PitchBlackSurfaceColor";
            string foregroundKey = theme == ThemeType.DarkGray ? "DarkGrayForegroundColor" : "PitchBlackForegroundColor";
            string secondaryTextKey = theme == ThemeType.DarkGray ? "LightGrayText" : "Gray500"; // Adjust secondary text based on theme

            Application.Current.Resources["AppBackgroundColor"] = Application.Current.Resources[backgroundKey];
            Application.Current.Resources["AppSurfaceColor"] = Application.Current.Resources[surfaceKey];
            Application.Current.Resources["AppForegroundColor"] = Application.Current.Resources[foregroundKey];
            Application.Current.Resources["AppSecondaryTextColor"] = Application.Current.Resources[secondaryTextKey]; // Apply secondary text color too

            Debug.WriteLine($"Theme applied: {theme}. Background: {Application.Current.Resources["AppBackgroundColor"]}, Surface: {Application.Current.Resources["AppSurfaceColor"]}, Foreground: {Application.Current.Resources["AppForegroundColor"]}");

            // Force Shell to re-evaluate styles if needed (might not be necessary with DynamicResource)
            if (MainPage is Shell shell)
            {
                // Attempt to force redraw if DynamicResource doesn't update shell immediately
                var currentBg = shell.BackgroundColor;
                shell.BackgroundColor = Colors.Transparent; // Temporary change
                shell.BackgroundColor = currentBg; // Restore (or re-bind if using DynamicResource)
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
            if (mainViewModel != null)
            {
                await mainViewModel.FinalizeAndSaveProgressAsync();
                Debug.WriteLine("App.OnSleep: FinalizeAndSaveProgressAsync completed.");
            }
            else
            {
                Debug.WriteLine("App.OnSleep: MainViewModel not resolved. Cannot save progress.");
            }
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