using Microsoft.UI.Xaml;
using System.Diagnostics;

namespace Tickly.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        Debug.WriteLine("Tickly.WinUI.App Constructor: Starting.");
        Debug.WriteLine("Tickly.WinUI.App Constructor: If you manually added Windows App SDK initialization (e.g., DeploymentManager.Initialize()), REMOVE IT for packaged apps.");

        try
        {
            this.InitializeComponent();
            Debug.WriteLine("Tickly.WinUI.App Constructor: InitializeComponent() called successfully.");
        }
        catch (Microsoft.UI.Xaml.Markup.XamlParseException xamlEx)
        {
            Debug.WriteLine($"CRITICAL XAML PARSE ERROR in Tickly.WinUI.App.InitializeComponent: {xamlEx.Message}");
            if (xamlEx.InnerException != null)
            {
                Debug.WriteLine($"  Inner Exception: {xamlEx.InnerException.GetType().FullName}: {xamlEx.InnerException.Message}");
                if (xamlEx.InnerException is System.IO.FileNotFoundException fnfEx)
                {
                    Debug.WriteLine($"    Inner FileNotFoundException Details: Could not load file or assembly '{fnfEx.FileName}'.");
                }
            }
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CRITICAL UNHANDLED ERROR in Tickly.WinUI.App.InitializeComponent: {ex.GetType().FullName} - {ex.Message}");
            if (ex.InnerException != null)
            {
                Debug.WriteLine($"  Inner Exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
            }
            throw;
        }
        Debug.WriteLine("Tickly.WinUI.App Constructor: Finished.");
    }

    protected override MauiApp CreateMauiApp()
    {
        Debug.WriteLine("Tickly.WinUI.App.CreateMauiApp: Starting.");
        try
        {
            var mauiApp = MauiProgram.CreateMauiApp();
            Debug.WriteLine("Tickly.WinUI.App.CreateMauiApp: MauiProgram.CreateMauiApp() returned.");
            return mauiApp;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CRITICAL ERROR in Tickly.WinUI.App.CreateMauiApp (calling MauiProgram.CreateMauiApp): {ex.GetType().FullName} - {ex.Message}");
            if (ex.InnerException != null)
            {
                Debug.WriteLine($"  Inner Exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                if (ex.InnerException is System.IO.FileNotFoundException fnfEx)
                {
                    Debug.WriteLine($"    Inner FileNotFoundException Details: Could not load file or assembly '{fnfEx.FileName}'.");
                }
            }
            throw;
        }
    }
}