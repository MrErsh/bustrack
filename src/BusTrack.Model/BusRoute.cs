namespace BusTrack.Model;

/// <summary>Автобусный маршрут с упорядоченными остановками по направлениям.</summary>
public sealed record BusRoute(
    string Id,
    string Number,
    string Name,
    IReadOnlyList<IReadOnlyList<Stop>> Directions);