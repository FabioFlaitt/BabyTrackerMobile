using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Plugin.LocalNotification;
using BabyTrackerMobile.Views;
using BabyTrackerMobile.ViewModels;
using BabyTrackerMobile.Services;

namespace BabyTrackerMobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseLocalNotification()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Serviços online (Firebase via REST)
        builder.Services.AddSingleton<FirebaseAuthService>();
        builder.Services.AddSingleton<FamilyService>();
        builder.Services.AddSingleton<CloudSyncService>();
        builder.Services.AddSingleton<ChatService>();
        builder.Services.AddSingleton<ChatNotificationService>();

        // ViewModels
        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<FeedingViewModel>();
        builder.Services.AddSingleton<DiaperViewModel>();
        builder.Services.AddSingleton<SleepViewModel>();
        builder.Services.AddSingleton<HistoryViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddTransient<MilestonesViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddSingleton<ChatViewModel>();

        // Pages
        builder.Services.AddSingleton<DashboardPage>();
        builder.Services.AddSingleton<FeedingPage>();
        builder.Services.AddSingleton<DiaperPage>();
        builder.Services.AddSingleton<SleepPage>();
        builder.Services.AddSingleton<HistoryPage>();
        builder.Services.AddSingleton<SettingsPage>();
        builder.Services.AddTransient<MilestonesPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<ChatPage>();

        return builder.Build();
    }
}
