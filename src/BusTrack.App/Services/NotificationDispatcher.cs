using BusTrack.Core;

namespace BusTrack.App.Services;

/// <summary>
/// Мост между движком трекинга и пушами: живёт весь срок процесса,
/// поэтому уведомления приходят, даже когда экран не активен.
/// Дедупликация событий уже сделана в NotificationDecider (Core).
/// </summary>
public sealed class NotificationDispatcher
{
    private readonly INotifier _notifier;

    public NotificationDispatcher(ITrackingService tracking, INotifier notifier)
    {
        _notifier = notifier;
        tracking.SnapshotReceived += OnSnapshot;
    }

    private void OnSnapshot(TrackingSnapshot snapshot)
    {
        foreach (var notification in snapshot.Notifications)
            _notifier.ShowArrival(notification);
    }
}