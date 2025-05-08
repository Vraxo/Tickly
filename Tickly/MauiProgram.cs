using Microsoft.Extensions.Logging;
using Tickly.ViewModels;
using Tickly.Views;
using CommunityToolkit.Mvvm.Messaging;
using Tickly.Services;

namespace Tickly;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("FluentSystemIcons-Regular.ttf", "FluentUI");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Register split persistence and import/export services
        builder.Services.AddSingleton<TaskStorageService>();
        builder.Services.AddSingleton<ProgressStorageService>();
        builder.Services.AddSingleton<DataExportService>(); // Added
        builder.Services.AddSingleton<DataImportService>(); // Added
        // REMOVED: builder.Services.AddSingleton<DataImportExportService>();

        builder.Services.AddSingleton<RepeatingTaskService>();
        builder.Services.AddSingleton<TaskVisualStateService>();

        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<StatsViewModel>();

        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<AddTaskPopupPage>();
        builder.Services.AddSingleton<SettingsPage>();
        builder.Services.AddSingleton<StatsPage>();

        return builder.Build();
    }
}