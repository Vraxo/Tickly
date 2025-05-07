using Microsoft.Extensions.Logging;
using Tickly.ViewModels; // Add using for ViewModels
using Tickly.Views;     // Add using for Views
using CommunityToolkit.Mvvm.Messaging;
using Tickly.Services; // If not already present

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
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Register Services
        builder.Services.AddSingleton<TaskPersistenceService>();
        builder.Services.AddSingleton<RepeatingTaskService>();    // Added
        builder.Services.AddSingleton<TaskVisualStateService>(); // Added

        // Register ViewModels (Singleton for MainViewModel, Transient for Popup is fine)
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>(); // Settings can also be singleton
        // No need to register AddTaskPopupPageViewModel if it's created within AddTaskPopupPage code-behind

        // Register Pages for Navigation
        builder.Services.AddSingleton<MainPage>(); // MainPage is usually Singleton
        builder.Services.AddTransient<AddTaskPopupPage>(); // Popup page should be Transient
        builder.Services.AddSingleton<SettingsPage>();    // Register SettingsPage

        // Register Messenger (if not already implicitly available via CommunityToolkit)
        // builder.Services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default); // Usually not needed explicitly

        return builder.Build();
    }
}
