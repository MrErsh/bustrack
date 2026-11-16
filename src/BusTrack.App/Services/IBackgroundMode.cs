namespace BusTrack.App.Services;

/// <summary>Платформенный режим «не убивать процесс, пока идёт трекинг».</summary>
public interface IBackgroundMode
{
    void Enable();
    void Disable();
}