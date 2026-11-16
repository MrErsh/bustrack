using BusTrack.Api;
using BusTrack.Model;

namespace BusTrack.Core;

/// <summary>Результат одного цикла опроса.</summary>
public sealed record TrackingSnapshot(
    IReadOnlyList<TrackedBus> Buses,
    IReadOnlyList<BusNotification> Notifications,
    DateTimeOffset UpdatedAt,
    string? Error);

/// <summary>Один цикл: позиции → свежесть → привязка к остановкам → прогнозы → уведомления.</summary>
public sealed class TrackingEngine
{
    /// <summary>Фиксы старше этого возраста считаем протухшими (GPS может залипать).</summary>
    public static readonly TimeSpan MaxFixAge = TimeSpan.FromMinutes(3);

    private readonly IGtfsRealtimeClient _api;
    private readonly IRouteCatalog _catalog;
    private readonly TimeProvider _time;
    private readonly NotificationDecider _decider = new();

    public TrackingEngine(IGtfsRealtimeClient api, IRouteCatalog catalog, TimeProvider? time = null)
    {
        _api = api;
        _catalog = catalog;
        _time = time ?? TimeProvider.System;
    }

    public async Task<TrackingSnapshot> PollAsync(TrackingSettings settings, CancellationToken ct = default)
    {
        var now = _time.GetUtcNow();
        var route = _catalog.Find(settings.RouteId);
        if (route is null)
            return new TrackingSnapshot([], [], now, $"Маршрут не найден в каталоге: {settings.RouteId}");

        try
        {
            var vehicles = await _api.GetVehiclesAsync(settings.RouteId, ct);
            var fresh = vehicles.Where(v => now - v.FixTime <= MaxFixAge).ToList();
            int? dirIndex = route.Directions.Count > 0
                ? Math.Clamp(settings.DirectionIndex, 0, route.Directions.Count - 1)
                : null;
            var buses = fresh
                .Select(v => StopMatcher.Match(v, route, dirIndex))
                .OrderBy(b => b.DistanceToStopMeters)
                .ToList();

            IReadOnlyList<BusNotification> notifications = [];
            if (settings.WatchedStopId is not null && fresh.Count > 0)
            {
                var arrivals = await _api.GetArrivalsAsync(fresh.Select(v => v.VehicleId).ToList(), ct);
                notifications = _decider.Decide(arrivals, settings, route, now);
            }

            return new TrackingSnapshot(buses, notifications, now, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new TrackingSnapshot([], [], now, e.Message);
        }
    }

    public void ResetNotifications() => _decider.Reset();
}