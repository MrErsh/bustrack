using Android.App;
using Android.Content;
using BusTrack.App.Services;
using BusTrack.Model;
using Application = Android.App.Application;

namespace BusTrack.App.Platforms.Android;

/// <summary>Локальные уведомления через NotificationManager. FCM-сервер не нужен.</summary>
public sealed class AndroidNotifier : INotifier
{
    public const string ChannelId = "bus_arrivals";
    private int _nextId = 1000;

    public AndroidNotifier()
    {
        var channel = new NotificationChannel(ChannelId, "Прибытие автобуса", NotificationImportance.High)
        {
            Description = "Уведомления о приближении автобуса к выбранной остановке"
        };
        GetManager().CreateNotificationChannel(channel);
    }

    public void ShowArrival(BusNotification notification)
    {
        // Android 13+: без разрешения уведомления молча не показываются — пропускаем.
        if (OperatingSystem.IsAndroidVersionAtLeast(33) &&
            Application.Context.CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) != global::Android.Content.PM.Permission.Granted)
            return;

        string text;
        if (notification.DistanceMeters is { } meters)
        {
            text = $"{notification.StopName} — автобус уже ближе {meters} м (борт {notification.VehicleId})";
        }
        else
        {
            var minutes = Math.Max(0, (int)Math.Ceiling(((notification.Arrival ?? DateTimeOffset.UtcNow) - DateTimeOffset.UtcNow).TotalMinutes));
            text = $"{notification.StopName} — через ~{minutes} мин (борт {notification.VehicleId})";
        }

        var intent = new Intent(Application.Context, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        var pending = PendingIntent.GetActivity(
            Application.Context, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var builder = new Notification.Builder(Application.Context, ChannelId)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetContentTitle("Автобус приближается")
            .SetContentText(text)
            .SetContentIntent(pending)
            .SetAutoCancel(true);

        GetManager().Notify(_nextId++, builder.Build());
    }

    private static NotificationManager GetManager()
        => (NotificationManager)Application.Context.GetSystemService(Context.NotificationService)!;
}