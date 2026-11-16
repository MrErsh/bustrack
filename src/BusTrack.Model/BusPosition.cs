namespace BusTrack.Model;

/// <summary>Сырой GPS-фикс автобуса из GTFS Realtime.</summary>
/// <param name="SpeedMps">Скорость в м/с (единица GTFS).</param>
/// <param name="Bearing">Азимут в градусах: 0 = север, 90 = восток.</param>
public sealed record BusPosition(
    string VehicleId,
    string? TripId,
    double Lat,
    double Lon,
    double Bearing,
    double SpeedMps,
    DateTimeOffset FixTime);