using BusTrack.Api;
using BusTrack.App.Services;
using BusTrack.Core;
using Microsoft.Extensions.Logging;

namespace BusTrack.App;

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

		// Api
		builder.Services.AddSingleton(ApiHttpClient.Create());
		builder.Services.AddSingleton<IGtfsRealtimeClient>(sp =>
			new GtfsRealtimeClient(sp.GetRequiredService<HttpClient>()));
		builder.Services.AddSingleton<IRouteCatalog, RouteCatalog>();

		// Core
		builder.Services.AddSingleton<TrackingEngine>();
		builder.Services.AddSingleton<ITrackingService, TrackingService>();

		// App-сервисы
		builder.Services.AddSingleton<ISettingsStore, PreferencesSettingsStore>();
		builder.Services.AddSingleton<INotifier, Platforms.Android.AndroidNotifier>();
		builder.Services.AddSingleton<IBackgroundMode, Platforms.Android.ForegroundServiceController>();
		builder.Services.AddSingleton<NotificationDispatcher>();

		// ViewModels и страницы
		builder.Services.AddTransient<ViewModels.MainViewModel>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<ViewModels.SettingsViewModel>();
		builder.Services.AddTransient<SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}