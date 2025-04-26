using Microsoft.Extensions.Logging;
using Tickly.ViewModels; // Add using for ViewModels
using Tickly.Views;     // Add using for Views
using Tickly.Services; // Add using for Services
using CommunityToolkit.Mvvm.Messaging; // If not already present

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
        builder.Services.AddSingleton<ITaskPersistenceService, TaskPersistenceService>(); // Register the new service

        // Register ViewModels (Singleton for MainViewModel, Transient for Popup is fine)
        builder.Services.AddSingleton<MainViewModel>();
        // No need to register AddTaskPopupPageViewModel if it's created within AddTaskPopupPage code-behind

        // Register Pages for Navigation
        builder.Services.AddSingleton<MainPage>(); // MainPage is usually Singleton
        builder.Services.AddTransient<AddTaskPopupPage>(); // Popup page should be Transient
        builder.Services.AddTransient<SettingsPage>(); // Make sure SettingsPage is registered if it uses DI (it doesn't currently)

        // Register Messenger (if not already implicitly available via CommunityToolkit)
        // builder.Services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default); // Usually not needed explicitly

        return builder.Build();
    }
}