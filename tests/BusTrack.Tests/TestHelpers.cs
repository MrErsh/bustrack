using BusTrack.Model;

namespace BusTrack.Tests;

/// <summary>Управляемое время для детерминированных тестов.</summary>
public sealed class FakeTimeProvider : TimeProvider
{
    public FakeTimeProvider(DateTimeOffset now) => Now = now;
    public DateTimeOffset Now { get; set; }
    public override DateTimeOffset GetUtcNow() => Now;
}

public static class TestData
{
    public static readonly DateTimeOffset T0 = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    public static Stop Stop(string id, double lat = 59.9, double lon = 30.3) => new(id, "Остановка " + id, lat, lon);

    public static BusRoute Route(string id = "370", params IReadOnlyList<Stop>[] directions)
        => new(id, "65", "Тестовый маршрут", directions);

    public static BusPosition Bus(string id, double lat = 59.9, double lon = 30.3, DateTimeOffset? fixTime = null)
        => new(id, "trip-" + id, lat, lon, Bearing: 0, SpeedMps: 0, FixTime: fixTime ?? T0);

    public static ArrivalPrediction Arrival(string vehicleId, string stopId, DateTimeOffset arrival, string? tripId = null)
        => new(vehicleId, tripId ?? "trip-" + vehicleId, stopId, arrival);
}