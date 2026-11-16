namespace BusTrack.Model;

/// <summary>
/// Событие «автобус приближается к наблюдаемой остановке».
/// Либо по прогнозу времени (Arrival), либо по расстоянию (DistanceMeters).
/// </summary>
public sealed record BusNotification(
    string VehicleId,
    string StopId,
    string StopName,
    DateTimeOffset? Arrival,
    int? DistanceMeters = null);