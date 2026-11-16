using BusTrack.Model;

namespace BusTrack.Core;

/// <summary>
/// Решает, о каких приближениях уведомлять. Чистая логика без таймеров и UI.
/// Дедупликация: один рейс — одно уведомление на остановку.
/// </summary>
public sealed class NotificationDecider
{
    private static readonly TimeSpan PruneAge = TimeSpan.FromHours(1);

    private readonly Dictionary<string, DateTimeOffset> _notified = [];

    public IReadOnlyList<BusNotification> Decide(
        IReadOnlyList<ArrivalPrediction> predictions,
        TrackingSettings settings,
        BusRoute route,
        DateTimeOffset now)
    {
        Prune(now);

        if (settings.NotifyBeforeMinutes is null)
            return [];

        var stop = FindWatchedStop(settings, route);
        if (stop is null)
            return [];

        var horizon = now.AddMinutes(settings.NotifyBeforeMinutes.Value);
        var result = new List<BusNotification>();
        foreach (var p in predictions)
        {
            if (p.StopId != settings.WatchedStopId)
                continue;
            // Просроченные прогнозы система отдаёт ещё какое-то время — отсекаем.
            if (p.Arrival <= now || p.Arrival > horizon)
                continue;

            var key = $"{p.VehicleId}|{p.TripId}|{p.StopId}";
            if (!_notified.TryAdd(key, p.Arrival))
                continue;

            result.Add(new BusNotification(p.VehicleId, p.StopId, stop.Name, p.Arrival));
        }
        return result;
    }

    /// <summary>
    /// Триггер по расстоянию: автобус ближе NotifyBeforeMeters к наблюдаемой остановке.
    /// Полезнее времени в пробках. Дедупликация своя (ключ с префиксом dist),
    /// чтобы не подавлять и не подавляться триггером по времени.
    /// </summary>
    public IReadOnlyList<BusNotification> DecideByDistance(
        IReadOnlyList<BusPosition> positions,
        TrackingSettings settings,
        BusRoute route,
        DateTimeOffset now)
    {
        Prune(now);

        if (settings.NotifyBeforeMeters is null)
            return [];

        var stop = FindWatchedStop(settings, route);
        if (stop is null)
            return [];

        var result = new List<BusNotification>();
        foreach (var p in positions)
        {
            var distance = GeoMath.DistanceMeters(p.Lat, p.Lon, stop.Lat, stop.Lon);
            if (distance > settings.NotifyBeforeMeters.Value)
                continue;

            var key = $"dist|{p.VehicleId}|{p.TripId}|{stop.Id}";
            if (!_notified.TryAdd(key, now))
                continue;

            result.Add(new BusNotification(p.VehicleId, stop.Id, stop.Name,
                Arrival: null, DistanceMeters: (int)Math.Round(distance)));
        }
        return result;
    }

    public void Reset() => _notified.Clear();

    // Остановка ищется только в выбранном направлении: у противоположного
    // направления могут быть остановки с теми же названиями, но другими ID.
    private static Stop? FindWatchedStop(TrackingSettings settings, BusRoute route)
    {
        if (settings.WatchedStopId is null || route.Directions.Count == 0)
            return null;

        var dirStops = route.Directions[Math.Clamp(settings.DirectionIndex, 0, route.Directions.Count - 1)];
        return dirStops.FirstOrDefault(s => s.Id == settings.WatchedStopId);
    }

    private void Prune(DateTimeOffset now)
    {
        var stale = _notified.Where(kv => kv.Value < now - PruneAge).Select(kv => kv.Key).ToList();
        foreach (var key in stale)
            _notified.Remove(key);
    }
}