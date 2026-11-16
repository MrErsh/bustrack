namespace BusTrack.Model;

/// <summary>Прогноз прибытия автобуса на остановку из GTFS Realtime trip updates.</summary>
public sealed record ArrivalPrediction(
    string VehicleId,
    string TripId,
    string StopId,
    DateTimeOffset Arrival);