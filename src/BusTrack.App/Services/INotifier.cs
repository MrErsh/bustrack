using BusTrack.Model;

namespace BusTrack.App.Services;

/// <summary>Локальные пуш-уведомления о приближении автобуса.</summary>
public interface INotifier
{
    void ShowArrival(BusNotification notification);
}