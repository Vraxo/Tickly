using Microsoft.Maui.LifecycleEvents;
using Tickly.ViewModels;
using Tickly.Services;
using System.Diagnostics;

namespace Tickly;

public partial class App : Application
{
    private const int WindowWidth = 450;
    private const int WindowHeight = 800;

    public App()
    {
        InitializeComponent();
        MainPage = new AppShell();
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
            if (mainViewModel is not null)
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
                catch (System.IO.FileNotFoundException fnfEx)
                {
                    System.Diagnostics.Debug.WriteLine($"App.CreateWindow: CRITICAL - WinRT Interop component missing: {fnfEx.FileName} - {fnfEx.Message}. Ensure Windows App SDK dependencies are correct. This specific catch is within CreateWindow; the startup error might be earlier.");
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