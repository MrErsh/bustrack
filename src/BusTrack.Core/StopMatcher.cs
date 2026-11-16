using BusTrack.Model;

namespace BusTrack.Core;

/// <summary>Привязка позиции автобуса к ближайшей остановке маршрута.</summary>
public static class StopMatcher
{
    /// <param name="directionIndex">Если задан — искать только среди остановок этого направления.</param>
    public static TrackedBus Match(BusPosition position, BusRoute route, int? directionIndex = null)
    {
        Stop? nearest = null;
        var direction = -1;
        var best = double.MaxValue;

        for (var d = 0; d < route.Directions.Count; d++)
        {
            if (directionIndex is not null && d != directionIndex)
                continue;

            foreach (var stop in route.Directions[d])
            {
                var distance = GeoMath.DistanceMeters(position.Lat, position.Lon, stop.Lat, stop.Lon);
                if (distance < best)
                {
                    best = distance;
                    nearest = stop;
                    direction = d;
                }
            }
        }

        return new TrackedBus(position, nearest, direction, best);
    }
}