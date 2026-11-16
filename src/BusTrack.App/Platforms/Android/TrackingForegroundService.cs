using Android.App;
using Android.Content;
using Android.OS;
using BusTrack.App.Services;
using BusTrack.Core;
using Microsoft.Extensions.DependencyInjection;
using Application = Android.App.Application;

namespace BusTrack.App.Platforms.Android;

/// <summary>
/// Foreground service: держит процесс живым, пока идёт трекинг,
/// и показывает постоянный статус-нотификайшен. Сам поллинг делает
/// TrackingService (singleton в DI) — сервис только продлевает ему жизнь.
/// </summary>
[Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
public sealed class TrackingForegroundService : Service
{
    public const string StatusChannelId = "tracking_status";
    private const int StatusNotificationId = 1;

    private ITrackingService? _tracking;

    public override void OnCreate()
    {
        base.OnCreate();

        var channel = new NotificationChannel(StatusChannelId, "Статус трекинга", NotificationImportance.Low)
        {
            Description = "Постоянное уведомление, пока идёт отслеживание автобусов"
        };
        var manager = (NotificationManager)GetSystemService(NotificationService)!;
        manager.CreateNotificationChannel(channel);

        _tracking = IPlatformApplication.Current!.Services.GetRequiredService<ITrackingService>();
        _tracking.SnapshotReceived += OnSnapshot;
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        StartForeground(StatusNotificationId, BuildNotification("Трекинг запущен"));
        // NotSticky: после убийства процесса трекинг не воскресаем сам —
        // пользователь запускает его кнопкой осознанно.
        return StartCommandResult.NotSticky;
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnDestroy()
    {
        if (_tracking is not null)
            _tracking.SnapshotReceived -= OnSnapshot;
        StopForeground(StopForegroundFlags.Remove);
        base.OnDestroy();
    }

    private void OnSnapshot(TrackingSnapshot snapshot)
    {
        var text = snapshot.Error is not null
            ? "Ошибка связи с порталом транспорта"
            : $"Автобусов на маршруте: {snapshot.Buses.Count}";
        var manager = (NotificationManager)GetSystemService(NotificationService)!;
        manager.Notify(StatusNotificationId, BuildNotification(text));
    }

    private Notification BuildNotification(string text)
    {
        var intent = new Intent(this, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        var pending = PendingIntent.GetActivity(this, 0, intent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        return new Notification.Builder(this, StatusChannelId)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetContentTitle("BusTrack")
            .SetContentText(text)
            .SetContentIntent(pending)
            .SetOngoing(true)
            .Build();
    }
}

/// <summary>Старт/остановка foreground service из ViewModel.</summary>
public sealed class ForegroundServiceController : IBackgroundMode
{
    public void Enable()
    {
        var intent = new Intent(Application.Context, typeof(TrackingForegroundService));
        Application.Context.StartForegroundService(intent);
    }

    public void Disable()
    {
        var intent = new Intent(Application.Context, typeof(TrackingForegroundService));
        Application.Context.StopService(intent);
    }
}