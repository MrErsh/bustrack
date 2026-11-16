namespace BusTrack.Model;

/// <summary>Автобус, привязанный к маршруту: ближайшая остановка и направление.</summary>
public sealed record TrackedBus(
    BusPosition Position,
    Stop? NearestStop,
    int DirectionIndex,
    double DistanceToStopMeters);