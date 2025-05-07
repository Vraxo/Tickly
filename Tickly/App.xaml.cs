using Microsoft.Maui.LifecycleEvents;
using Tickly.ViewModels;
using Tickly.Services;
using System.Diagnostics;
using Microsoft.Maui.Controls;
using Microsoft.Maui;
using System;
using System.IO; // Required for Path
using System.Threading.Tasks; // Required for Task

#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;
#elif MACCATALYST
using Foundation;
using AppKit;
#endif

namespace Tickly;

public partial class App : Application
{
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
        Debug.WriteLine("App.OnStart: Application started.");

#if DEBUG
        await OpenDataDirectory();
#endif
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

    private async Task OpenDataDirectory()
    {
        string dataPath = FileSystem.AppDataDirectory;
        Debug.WriteLine($"[DEBUG] Application Data Directory: {dataPath}");

        if (!Directory.Exists(dataPath))
        {
            Debug.WriteLine($"[DEBUG] Data directory does not exist. Creating: {dataPath}");
            try
            {
                Directory.CreateDirectory(dataPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DEBUG] Failed to create data directory: {ex.Message}");
                return; // Cannot open if creation failed
            }
        }

        try
        {
#if WINDOWS
            Debug.WriteLine("[DEBUG] Attempting to open directory on Windows...");
            Process.Start("explorer.exe", $"\"{dataPath}\"");
            await Task.CompletedTask; // Keep async signature
#elif MACCATALYST
            Debug.WriteLine("[DEBUG] Attempting to open directory on MacCatalyst...");
            Process.Start("open", $"\"{dataPath}\"");
             await Task.CompletedTask; // Keep async signature
#elif ANDROID || IOS
            Debug.WriteLine("[DEBUG] Opening data directory automatically is not supported on this mobile platform.");
            await Task.CompletedTask; // Keep async signature
#else
            Debug.WriteLine("[DEBUG] Platform not configured for opening data directory automatically.");
             await Task.CompletedTask; // Keep async signature
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DEBUG] Failed to open data directory: {ex.Message}");
            // Optionally show an alert to the user in debug mode if it fails
        }
    }


    protected override Window CreateWindow(IActivationState activationState)
    {
        Window window = base.CreateWindow(activationState);

#if WINDOWS
        window.Created += (s, e) =>
        {
            var mauiWindow = s as Window;
            if (mauiWindow == null)
            {
                return;
            }

            var handler = mauiWindow.Handler;
            if (handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
            {
                try
                {
                    var hwnd = WindowNative.GetWindowHandle(nativeWindow);
                    if (hwnd == IntPtr.Zero)
                    {
                        return;
                    }

                    Microsoft.UI.WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                    if (windowId.Value == 0)
                    {
                        return;
                    }

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
                        appWindow.Title = "Tickly Task Manager";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error resizing or setting title for window: {ex.Message}");
                }
            }
        };
#elif MACCATALYST
        // You might need to handle window creation/sizing differently for MacCatalyst if needed
        // Example: Setting a fixed size (may require additional checks for handler availability)
        // window.Width = WindowWidth;
        // window.Height = WindowHeight;
        // Consider centering logic similar to Windows if desired
#endif

        return window;
    }
}