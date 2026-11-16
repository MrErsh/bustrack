using BusTrack.Model;

namespace BusTrack.Core;

/// <summary>Привязка позиции автобуса к ближайшей остановке маршрута.</summary>
public static class StopMatcher
{
    public static TrackedBus Match(BusPosition position, BusRoute route)
    {
        Stop? nearest = null;
        var direction = -1;
        var best = double.MaxValue;

        for (var d = 0; d < route.Directions.Count; d++)
        {
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