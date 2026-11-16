namespace BusTrack.Model;

/// <summary>Событие «автобус приближается к наблюдаемой остановке».</summary>
public sealed record BusNotification(
    string VehicleId,
    string StopId,
    string StopName,
    DateTimeOffset Arrival);