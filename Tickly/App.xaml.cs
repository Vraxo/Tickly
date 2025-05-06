// App.xaml.cs
using Microsoft.Maui.LifecycleEvents; // Might be needed depending on exact MAUI version/setup
using Tickly.ViewModels;
using Tickly.Services; // Added for TaskPersistenceService if directly used, or for general service access
using System.Diagnostics;

namespace Tickly; // Make sure this namespace matches your project

public partial class App : Application
{
    // Define desired size constants
    const int WindowWidth = 450;
    const int WindowHeight = 800;

    public App()
    {
        InitializeComponent();

        MainPage = new AppShell();
    }

    protected override async void OnStart()
    {
        base.OnStart();
        // Optional: Logic to check and save progress for a missed day could go here,
        // but it's complex as we wouldn't know the percentage.
        // The current MainViewModel logic aims to keep today's progress up-to-date.
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
        // Optional: Could trigger a progress check/update here if the day might have changed
        // while the app was asleep for an extended period.
        // However, MainViewModel's continuous updates should handle this if it's running.
    }

    protected override Window CreateWindow(IActivationState activationState)
    {
        Window window = base.CreateWindow(activationState);

        // --- WINDOWS SPECIFIC RESIZING ---
#if WINDOWS
        window.Created += (s, e) =>
        {
            // Ensure sender is a Window
            var mauiWindow = s as Window;
            if (mauiWindow == null) return;

            // Get the Handler and Platform Window (Microsoft.UI.Xaml.Window)
            var handler = mauiWindow.Handler;
            if (handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
            {
                try
                {
                    // Get the HWND (Window Handle) for the WinUI window
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                    if (hwnd == IntPtr.Zero) return;

                    // Get the WindowId from the HWND
                    Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                    if (windowId.Value == 0) return; // Invalid WindowId

                    // Get the AppWindow from the WindowId
                    Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                    if (appWindow != null)
                    {
                         // Get the primary display area
                         Microsoft.UI.Windowing.DisplayArea displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
                        if (displayArea != null)
                        {
                            // Calculate centered position
                            var mainMonitorWorkArea = displayArea.WorkArea;
                            var centeredX = (mainMonitorWorkArea.Width - WindowWidth) / 2;
                            var centeredY = (mainMonitorWorkArea.Height - WindowHeight) / 2;

                            // Move and resize the window
                            appWindow.MoveAndResize(new Windows.Graphics.RectInt32(centeredX, centeredY, WindowWidth, WindowHeight));
                        }
                        else
                        {
                            // Fallback if display area info not available - just resize
                            appWindow.ResizeClient(new Windows.Graphics.SizeInt32(WindowWidth, WindowHeight));
                        }

                        // --- Optional: Prevent User Resizing ---
                        // If you want to lock the window size:
                        // if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter p)
                        // {
                        //     p.IsResizable = false;
                        //     p.IsMaximizable = false;
                        // }
                        // --- End Optional ---
                    }
                }
                catch (Exception ex)
                {
                    // Log or handle potential exceptions during window manipulation
                    System.Diagnostics.Debug.WriteLine($"Error resizing window: {ex.Message}");
                }
            }
        };
#endif
        // --- END WINDOWS SPECIFIC ---

        return window;
    }
}
