using Microsoft.Extensions.Logging;
using Tickly.ViewModels;
using Tickly.Views;
using CommunityToolkit.Mvvm.Messaging;
using Tickly.Services;
using CommunityToolkit.Maui;
using Microcharts.Maui;
using System.Diagnostics;

namespace Tickly;

public static class MauiProgram
{
    static MauiProgram()
    {
        Debug.WriteLine("MauiProgram: Static constructor executing.");
        Debug.WriteLine("MauiProgram: Static constructor finished.");
    }

    public static MauiApp CreateMauiApp()
    {
        Debug.WriteLine("MauiProgram.CreateMauiApp: Method started.");
        try
        {
            Debug.WriteLine("MauiProgram.CreateMauiApp: Creating MauiAppBuilder...");
            var builder = MauiApp.CreateBuilder();
            Debug.WriteLine("MauiProgram.CreateMauiApp: MauiAppBuilder created.");

            Debug.WriteLine("MauiProgram.CreateMauiApp: Configuring UseMauiApp<App>...");
            builder.UseMauiApp<App>();
            Debug.WriteLine("MauiProgram.CreateMauiApp: UseMauiApp<App> configured.");

            Debug.WriteLine("MauiProgram.CreateMauiApp: Configuring UseMauiCommunityToolkit...");
            builder.UseMauiCommunityToolkit();
            Debug.WriteLine("MauiProgram.CreateMauiApp: UseMauiCommunityToolkit configured.");

            Debug.WriteLine("MauiProgram.CreateMauiApp: Configuring UseMicrocharts...");
            builder.UseMicrocharts();
            Debug.WriteLine("MauiProgram.CreateMauiApp: UseMicrocharts configured.");

            Debug.WriteLine("MauiProgram.CreateMauiApp: Configuring fonts...");
            builder.ConfigureFonts(fonts =>
            {
                Debug.WriteLine("MauiProgram.CreateMauiApp: Adding OpenSans-Regular.ttf...");
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                Debug.WriteLine("MauiProgram.CreateMauiApp: Adding OpenSans-Semibold.ttf...");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
            Debug.WriteLine("MauiProgram.CreateMauiApp: Fonts configured.");

#if DEBUG
            Debug.WriteLine("MauiProgram.CreateMauiApp: Adding Debug Logging...");
            builder.Logging.AddDebug();
            Debug.WriteLine("MauiProgram.CreateMauiApp: Debug Logging added.");
#endif

            Debug.WriteLine("MauiProgram.CreateMauiApp: Registering TaskPersistenceService...");
            builder.Services.AddSingleton<TaskPersistenceService>();
            Debug.WriteLine("MauiProgram.CreateMauiApp: Registering RepeatingTaskService...");
            builder.Services.AddSingleton<RepeatingTaskService>();
            Debug.WriteLine("MauiProgram.CreateMauiApp: Registering TaskVisualStateService...");
            builder.Services.AddSingleton<TaskVisualStateService>();

            Debug.WriteLine("MauiProgram.CreateMauiApp: Registering MainViewModel...");
            builder.Services.AddSingleton<MainViewModel>();
            Debug.WriteLine("MauiProgram.CreateMauiApp: Registering SettingsViewModel...");
            builder.Services.AddSingleton<SettingsViewModel>();
            Debug.WriteLine("MauiProgram.CreateMauiApp: Registering ProgressViewModel...");
            builder.Services.AddSingleton<ProgressViewModel>();

            Debug.WriteLine("MauiProgram.CreateMauiApp: Registering MainPage...");
            builder.Services.AddSingleton<MainPage>();
            Debug.WriteLine("MauiProgram.CreateMauiApp: Registering AddTaskPopupPage...");
            builder.Services.AddTransient<AddTaskPopupPage>();
            Debug.WriteLine("MauiProgram.CreateMauiApp: Registering SettingsPage...");
            builder.Services.AddSingleton<SettingsPage>();
            Debug.WriteLine("MauiProgram.CreateMauiApp: Registering ProgressPage...");
            builder.Services.AddSingleton<ProgressPage>();
            Debug.WriteLine("MauiProgram.CreateMauiApp: All services and pages registered.");

            Debug.WriteLine("MauiProgram.CreateMauiApp: Building MauiApp...");
            MauiApp app = builder.Build();
            Debug.WriteLine("MauiProgram.CreateMauiApp: MauiApp built successfully.");
            return app;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CRITICAL ERROR in MauiProgram.CreateMauiApp: {ex.GetType().FullName} - {ex.Message}");
            Exception? currentEx = ex;
            int indentLevel = 0;
            while (currentEx != null)
            {
                string indent = new(' ', indentLevel * 2);
                Debug.WriteLine($"{indent}Exception (level {indentLevel}): {currentEx.GetType().FullName}: {currentEx.Message}");
                if (currentEx is System.IO.FileNotFoundException fnfEx)
                {
                    Debug.WriteLine($"{indent}  FileNotFoundException Details: Could not load file or assembly '{fnfEx.FileName}'. FusionLog: {fnfEx.FusionLog}");
                }
                else if (currentEx is TypeInitializationException tiEx)
                {
                    Debug.WriteLine($"{indent}  TypeInitializationException for Type: {tiEx.TypeName}");
                }
                Debug.WriteLine($"{indent}  StackTrace: {currentEx.StackTrace}");

                currentEx = currentEx.InnerException;
                indentLevel++;
            }
            throw;
        }
    }
}