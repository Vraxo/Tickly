using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using System.Diagnostics;
using Tickly.Models;
using Windows.Graphics;

using WinRT.Interop;

// Keep only one set of using statements
using Microsoft.Maui.LifecycleEvents;
using Tickly.ViewModels;
using Tickly.Services;
using System.Diagnostics;
using Microsoft.Maui.Controls;
using Microsoft.Maui;
using System;
using System.IO; // Required for Path
using System.Threading.Tasks; // Required for Task
using CommunityToolkit.Mvvm.Messaging; // Required for messaging
using Tickly.Models; // Required for DarkModeBackgroundType
using Tickly.Messages; // Required for DarkModeBackgroundChangedMessage

// Keep only one set of platform directives
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
    // Define constants only ONCE
    const int WindowWidth = 450;
    const int WindowHeight = 800;

    public App()
    {
        InitializeComponent();

        // Set the initial theme based on saved preference *before* MainPage is created
        ApplyDarkModePreference();

        MainPage = new AppShell();

        // Register to receive theme change messages AFTER MainPage is set
        WeakReferenceMessenger.Default.Register<DarkModeBackgroundChangedMessage>(this, (r, m) => ApplyDarkModePreference());
    }

    // Define ApplyDarkModePreference only ONCE
    private void ApplyDarkModePreference()
    {
        try
        {
            if (!MainThread.IsMainThread)
            {
                MainThread.BeginInvokeOnMainThread(ApplyDarkModePreference);
                return;
            }

            var selectedBgType = AppSettings.SelectedDarkModeBackground;
            Color effectiveColor;
            Brush effectiveBrush;

            if (selectedBgType == DarkModeBackgroundType.PureBlack)
            {
                effectiveColor = (Color)this.Resources["AppDarkBackgroundBlack"];
                effectiveBrush = (Brush)this.Resources["AppDarkBackgroundBlackBrush"];
                Debug.WriteLine("App.ApplyDarkModePreference: Applying PureBlack background.");
            }
            else // Default to OffBlack
            {
                effectiveColor = (Color)this.Resources["AppDarkBackgroundOffBlack"];
                effectiveBrush = (Brush)this.Resources["AppDarkBackgroundOffBlackBrush"];
                Debug.WriteLine("App.ApplyDarkModePreference: Applying OffBlack background.");
            }

            // Update the dynamic resources
            this.Resources["AppEffectiveDarkBackgroundColor"] = effectiveColor;
            this.Resources["AppEffectiveDarkBackgroundBrush"] = effectiveBrush;

            Debug.WriteLine($"App.ApplyDarkModePreference: Updated AppEffectiveDarkBackground resources.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"App.ApplyDarkModePreference: Error updating theme resources: {ex.Message}");
        }
    }

    // Define OnStart only ONCE
    protected override async void OnStart()
    {
        base.OnStart();
        Debug.WriteLine("App.OnStart: Application started.");

#if DEBUG
        await OpenDataDirectory();
#endif
    }

    // Define OnSleep only ONCE
    protected override async void OnSleep()
    {
        base.OnSleep();
        Debug.WriteLine("App.OnSleep: Application is going to sleep. Saving final progress.");
        try
        {
            // Use GetService safely
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

    // Define OnResume only ONCE
    protected override async void OnResume()
    {
        base.OnResume();
        Debug.WriteLine("App.OnResume: Application is resuming.");
    }

    // Define OpenDataDirectory only ONCE
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

    // Define CreateWindow only ONCE
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
                    IntPtr hwnd = WindowNative.GetWindowHandle(nativeWindow);
                    if (hwnd == IntPtr.Zero) return;

                    WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                    if (windowId.Value == 0) return;

                    AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
                    if (appWindow != null)
                    {
                        DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
                        if (displayArea != null)
                        {
                            var mainMonitorWorkArea = displayArea.WorkArea;
                            // Use the constants defined at the top of the class
                            var centeredX = (mainMonitorWorkArea.Width - WindowWidth) / 2;
                            var centeredY = (mainMonitorWorkArea.Height - WindowHeight) / 2;
                            appWindow.MoveAndResize(new RectInt32(centeredX, centeredY, WindowWidth, WindowHeight));
                        }
                        else
                        {
                            // Fallback if DisplayArea is null for some reason
                            appWindow.ResizeClient(new SizeInt32(WindowWidth, WindowHeight));
                        }
                        appWindow.Title = "Tickly Task Manager";
                        // Optional: Prevent resizing
                        // if (appWindow.Presenter is OverlappedPresenter p)
                        // {
                        //    p.IsResizable = false;
                        //    p.IsMaximizable = false;
                        // }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error resizing or setting title for window: {ex.Message}");
                }
            }
        };
#elif MACCATALYST
        // Mac window sizing/positioning logic if needed
#endif

        return window;
    }
}
