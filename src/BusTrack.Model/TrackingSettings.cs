namespace BusTrack.Model;

/// <summary>Пользовательские настройки трекинга (хранятся между запусками).</summary>
/// <param name="WatchedStopId">Остановка, о приближении к которой уведомлять; null — не уведомлять.</param>
/// <param name="NotifyBeforeMinutes">За сколько минут до прибытия уведомлять.</param>
/// <param name="DirectionIndex">Индекс направления маршрута (остановки и уведомления — только по нему).</param>
public sealed record TrackingSettings(
    string RouteId,
    string? WatchedStopId,
    int NotifyBeforeMinutes,
    int DirectionIndex = 0)
{
    public static TrackingSettings Default { get; } = new("370", null, 5);
}