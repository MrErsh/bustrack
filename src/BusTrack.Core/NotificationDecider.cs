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

        if (settings.WatchedStopId is null)
            return [];

        // Остановка ищется только в выбранном направлении: у противоположного
        // направления могут быть остановки с теми же названиями, но другими ID.
        var dirStops = route.Directions.Count > 0
            ? route.Directions[Math.Clamp(settings.DirectionIndex, 0, route.Directions.Count - 1)]
            : [];
        var stop = dirStops.FirstOrDefault(s => s.Id == settings.WatchedStopId);
        if (stop is null)
            return [];

        var horizon = now.AddMinutes(settings.NotifyBeforeMinutes);
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

    public void Reset() => _notified.Clear();

    private void Prune(DateTimeOffset now)
    {
        var stale = _notified.Where(kv => kv.Value < now - PruneAge).Select(kv => kv.Key).ToList();
        foreach (var key in stale)
            _notified.Remove(key);
    }
}