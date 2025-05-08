using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.LifecycleEvents;
using Tickly.ViewModels;
using Tickly.Services;
using System.Diagnostics;
using Tickly.Messages;
using Tickly.Models;
using System.Collections.Generic; // For List
using System.Linq; // For Linq operations on list

#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using Microsoft.UI.Composition.SystemBackdrops;
using WinRT;
#endif

using MauiColors = Microsoft.Maui.Graphics.Colors;

namespace Tickly;

public partial class App : Microsoft.Maui.Controls.Application
{
    private const int WindowWidth = 450;
    private const int WindowHeight = 800;
    private readonly ThemeService _themeService;

#if WINDOWS
    private Microsoft.UI.Xaml.Window? _nativeWindow;
    private MicaController? _micaController;
    private SystemBackdropConfiguration? _backdropConfiguration;
    // Re-introduced page tracking
    private readonly List<WeakReference<Page>> _shellManagedPages = [];
#endif

    public App(ThemeService themeService)
    {
        InitializeComponent();
        _themeService = themeService;

        // 1. Apply theme resources
        _themeService.ApplyTheme(AppSettings.SelectedTheme);

        // 2. Apply system background override (may change page backgrounds)
        // Defer slightly to allow MainPage/Shell to initialize better
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
        {
            ApplySystemBackgroundOverride(AppSettings.UseWindowsSystemBackground);
        });

        MainPage = new AppShell();

#if WINDOWS
        if (MainPage is Shell shell)
        {
            shell.Navigated += Shell_Navigated;
            // Attempt initial registration after a short delay
            shell.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(50), () => RegisterCurrentShellPage(shell));
        }
#endif

        WeakReferenceMessenger.Default.Register<ThemeChangedMessage>(this, HandleThemeChanged);
        WeakReferenceMessenger.Default.Register<SystemBackgroundChangedMessage>(this, HandleSystemBackgroundChanged);
    }

#if WINDOWS
    private void Shell_Navigated(object? sender, ShellNavigatedEventArgs e)
    {
        if (sender is Shell shell)
        {
            RegisterCurrentShellPage(shell); // Register the page that was navigated TO
        }
        CleanupWeakReferences();
    }

    // Renamed and simplified registration
    private void RegisterCurrentShellPage(Shell shell)
    {
        var currentPage = shell.CurrentPage;
        if (currentPage == null) return;

        if (!_shellManagedPages.Any(wr => wr.TryGetTarget(out var target) && target == currentPage))
        {
            _shellManagedPages.Add(new WeakReference<Page>(currentPage));
            Debug.WriteLine($"Registered Shell Page: {currentPage.GetType().Name}");
            // Apply current override state immediately to the newly registered page
            ApplySystemBackgroundOverrideToPage(currentPage, AppSettings.UseWindowsSystemBackground);
        }
    }

    private void CleanupWeakReferences()
    {
        _shellManagedPages.RemoveAll(wr => !wr.TryGetTarget(out _));
        // Debug.WriteLine($"CleanupWeakReferences: {_shellManagedPages.Count} references remain.");
    }

    // Consolidated direct background setting logic
    private void ApplySystemBackgroundOverrideToPage(Page page, bool useSystemBackground)
    {
        if (page == null) return;

        MainThread.BeginInvokeOnMainThread(() => // Ensure UI updates on main thread
        {
            Color targetColor;
            if (useSystemBackground)
            {
                targetColor = MauiColors.Transparent;
                // Debug.WriteLine($"ApplySystemBackgroundOverrideToPage: Setting {page.GetType().Name} BG -> Transparent.");
            }
            else
            {
                // Fetch the *actual* theme color from the resource dictionary
                if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue("AppBackgroundColor", out var themeColor) == true && themeColor is Color color)
                {
                    targetColor = color;
                    // Debug.WriteLine($"ApplySystemBackgroundOverrideToPage: Setting {page.GetType().Name} BG -> Theme Color ({targetColor}).");
                }
                else
                {
                    targetColor = MauiColors.Black; // Fallback
                    Debug.WriteLine($"ApplySystemBackgroundOverrideToPage: Theme color resource not found for {page.GetType().Name}. Falling back to Black.");
                }
            }
            page.BackgroundColor = targetColor;
        });
    }
#endif

    private void HandleThemeChanged(object recipient, ThemeChangedMessage message)
    {
        // 1. Apply the theme resource changes
        _themeService.ApplyTheme(message.Value);
        // 2. Re-apply the background override based on the *current* setting
        ApplySystemBackgroundOverride(AppSettings.UseWindowsSystemBackground);
    }

    private void HandleSystemBackgroundChanged(object recipient, SystemBackgroundChangedMessage message)
    {
        // Only apply the background override when this specific setting changes
        ApplySystemBackgroundOverride(message.Value);
    }

    // Renamed: This now specifically handles the background override logic
    private void ApplySystemBackgroundOverride(bool useSystemBackground)
    {
#if WINDOWS
        // Update Mica First
        bool micaSuccess = TrySetMica(useSystemBackground);

        // Apply background color override to known pages *after* Mica state is set
        MainThread.BeginInvokeOnMainThread(() => // Run UI updates on main thread
        {
            CleanupWeakReferences();
            foreach (var weakRef in _shellManagedPages)
            {
                if (weakRef.TryGetTarget(out var page))
                {
                     // If Mica failed to apply, force non-transparent background regardless of setting
                    ApplySystemBackgroundOverrideToPage(page, useSystemBackground && micaSuccess);
                }
            }

            // Apply to Shell itself
            if (MainPage is Shell shell)
            {
                ApplySystemBackgroundOverrideToPage(shell, useSystemBackground && micaSuccess);
                Debug.WriteLine($"ApplySystemBackgroundOverride: Updated Shell Background (UseSystemBG: {useSystemBackground}, MicaSuccess: {micaSuccess})");
            }
        });
#endif
    }

    protected override void OnStart() { base.OnStart(); }
    protected override async void OnSleep()
    {
        base.OnSleep();
        try { IPlatformApplication.Current?.Services?.GetService<MainViewModel>()?.FinalizeAndSaveProgressAsync().FireAndForgetSafeAsync(); }
        catch (Exception ex) { Debug.WriteLine($"App.OnSleep: Error: {ex.Message}"); }
    }
    protected override void OnResume() { base.OnResume(); }

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
         if (_micaController != null) { _micaController.Dispose(); _micaController = null; }
         _nativeWindow = null; _backdropConfiguration = null;
         _shellManagedPages.Clear();
          if (MainPage is Shell shell) shell.Navigated -= Shell_Navigated;
         Debug.WriteLine("Window_Stopped: Cleaned up WinUI resources.");
#endif
    }

#if WINDOWS
     private void Window_Created_Windows(object? sender, EventArgs e)
     {
         var mauiWindow = sender as Microsoft.Maui.Controls.Window;
         if (mauiWindow == null) return;
         _nativeWindow = mauiWindow.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
         if (_nativeWindow != null)
         {
              // Initial Mica attempt deferred to ApplySystemBackgroundOverride in App constructor

              // Resizing Logic
              try { PositionAndResizeWindow(); } catch (Exception ex) { Debug.WriteLine($"Window_Created_Windows: Error resizing: {ex.Message}"); }
         }
     }

     private void PositionAndResizeWindow()
     {
         if (_nativeWindow == null) return;
         IntPtr hwnd = WindowNative.GetWindowHandle(_nativeWindow);
         if (hwnd == IntPtr.Zero) return;
         WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
         if (windowId.Value == 0) return;
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
             else { appWindow.ResizeClient(new Windows.Graphics.SizeInt32(WindowWidth, WindowHeight)); }
             Debug.WriteLine($"PositionAndResizeWindow: Window positioned/resized.");
         }
     }


    private bool TrySetMica(bool enable)
    {
        if (_nativeWindow == null) { Debug.WriteLine("TrySetMica: Native window is null."); return false; }
        if (!MicaController.IsSupported())
        {
             if (enable) Debug.WriteLine("TrySetMica: Mica not supported.");
             if (_micaController != null) { _micaController.Dispose(); _micaController = null; }
             return false;
        }

        if (enable)
        {
            if (_micaController == null)
            {
                _micaController = new MicaController();
                _backdropConfiguration ??= new SystemBackdropConfiguration();
                _nativeWindow.Activated -= Window_Activated; _nativeWindow.Activated += Window_Activated;
                _nativeWindow.Closed -= Window_Closed; _nativeWindow.Closed += Window_Closed;
                if(_nativeWindow.Content is FrameworkElement fwE) fwE.ActualThemeChanged -= Window_ThemeChanged;
                if(_nativeWindow.Content is FrameworkElement fwE2) fwE2.ActualThemeChanged += Window_ThemeChanged;
                _backdropConfiguration.IsInputActive = _nativeWindow.Visible;
                SetConfigurationSourceTheme();
                _micaController.SetSystemBackdropConfiguration(_backdropConfiguration);
                bool result = _micaController.AddSystemBackdropTarget(_nativeWindow.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                if(result) Debug.WriteLine("TrySetMica: Mica ENABLED."); else Debug.WriteLine("TrySetMica: AddSystemBackdropTarget FAILED.");
                return result;
            }
            SetConfigurationSourceTheme(); // Refresh theme config if already enabled
            return true; // Already enabled
        }
        else // Disabling Mica
        {
            if (_micaController != null)
            {
                _micaController.Dispose(); _micaController = null;
                _nativeWindow.Activated -= Window_Activated;
                _nativeWindow.Closed -= Window_Closed;
                 if(_nativeWindow.Content is FrameworkElement fwElement) fwElement.ActualThemeChanged -= Window_ThemeChanged;
                Debug.WriteLine("TrySetMica: Mica DISABLED.");
            }
            return true;
        }
    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (_backdropConfiguration != null)
            _backdropConfiguration.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        if (_micaController != null) { _micaController.Dispose(); _micaController = null; }
        if (sender is Microsoft.UI.Xaml.Window window)
        {
            window.Activated -= Window_Activated; window.Closed -= Window_Closed;
            if(window.Content is FrameworkElement content) content.ActualThemeChanged -= Window_ThemeChanged;
        }
         _nativeWindow = null; _backdropConfiguration = null; _shellManagedPages.Clear();
         if(MainPage is Shell shell) shell.Navigated -= Shell_Navigated;
         Debug.WriteLine("Window_Closed: Cleaned up Mica controller and handlers.");
    }

    private void Window_ThemeChanged(FrameworkElement sender, object args) => SetConfigurationSourceTheme();

    private void SetConfigurationSourceTheme()
    {
        if (_backdropConfiguration == null) return;

        var currentMauiTheme = Microsoft.Maui.Controls.Application.Current?.UserAppTheme ?? AppTheme.Unspecified;
        ElementTheme? frameworkElementTheme = (_nativeWindow?.Content as FrameworkElement)?.ActualTheme;

        if (frameworkElementTheme.HasValue)
        {
             _backdropConfiguration.Theme = frameworkElementTheme.Value switch {
                 ElementTheme.Dark => SystemBackdropTheme.Dark, ElementTheme.Light => SystemBackdropTheme.Light, _ => SystemBackdropTheme.Default
             };
        }
        else if (currentMauiTheme != AppTheme.Unspecified)
        {
             _backdropConfiguration.Theme = currentMauiTheme switch {
                 AppTheme.Dark => SystemBackdropTheme.Dark, AppTheme.Light => SystemBackdropTheme.Light, _ => SystemBackdropTheme.Default
             };
        }
        else { _backdropConfiguration.Theme = SystemBackdropTheme.Default; }
    }
#endif
}

internal static class TaskExtensions
{
    internal static async void FireAndForgetSafeAsync(this Task task, Action<Exception>? errorHandler = null)
    {
        try { await task; }
        catch (Exception ex) { Debug.WriteLine($"FireAndForgetSafeAsync: Error: {ex}"); errorHandler?.Invoke(ex); }
    }
}