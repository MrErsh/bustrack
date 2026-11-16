namespace BusTrack.Model;

/// <summary>Остановка маршрута.</summary>
public sealed record Stop(string Id, string Name, double Lat, double Lon);