using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BusTrack.Model;

namespace BusTrack.Api;

public interface IRouteCatalog
{
    IReadOnlyList<BusRoute> Routes { get; }
    BusRoute? Find(string routeId);
}

/// <summary>Каталог всех автобусных маршрутов СПб из встроенного GTFS-дампа.</summary>
public sealed partial class RouteCatalog : IRouteCatalog
{
    private static readonly Regex NumberSplit = new(@"(\d+)", RegexOptions.Compiled);

    private readonly Dictionary<string, BusRoute> _byId;

    public RouteCatalog()
    {
        using var stream = typeof(RouteCatalog).Assembly
            .GetManifestResourceStream("BusTrack.Api.Resources.bustrack_catalog.json")
            ?? throw new InvalidOperationException("Embedded catalog not found");

        var dto = JsonSerializer.Deserialize(stream, CatalogJsonContext.Default.CatalogDto)
            ?? throw new InvalidOperationException("Catalog is empty");

        var routes = new List<BusRoute>(dto.Routes.Count);
        foreach (var r in dto.Routes)
        {
            var directions = dto.Stops.TryGetValue(r.Id, out var dirs)
                ? dirs.Select(d => (IReadOnlyList<Stop>)d.Select(s => new Stop(s.Id, s.Name, s.Lat, s.Lon)).ToList()).ToList()
                : [];
            routes.Add(new BusRoute(r.Id, r.Num, r.Name, directions));
        }

        routes.Sort((a, b) => NaturalCompare(a.Number, b.Number));
        Routes = routes;
        _byId = routes.ToDictionary(r => r.Id);
    }

    public IReadOnlyList<BusRoute> Routes { get; }

    public BusRoute? Find(string routeId) => _byId.GetValueOrDefault(routeId);

    /// <summary>Естественная сортировка номеров: 2 &lt; 10 &lt; 65К.</summary>
    internal static int NaturalCompare(string a, string b)
    {
        var pa = NumberSplit.Split(a);
        var pb = NumberSplit.Split(b);
        for (var i = 0; i < Math.Min(pa.Length, pb.Length); i++)
        {
            int cmp;
            if (long.TryParse(pa[i], out var na) && long.TryParse(pb[i], out var nb))
                cmp = na.CompareTo(nb);
            else
                cmp = string.Compare(pa[i], pb[i], StringComparison.OrdinalIgnoreCase);
            if (cmp != 0)
                return cmp;
        }
        return pa.Length.CompareTo(pb.Length);
    }
}

internal sealed record CatalogDto(
    [property: JsonPropertyName("routes")] List<CatalogRouteDto> Routes,
    [property: JsonPropertyName("stops")] Dictionary<string, List<List<CatalogStopDto>>> Stops);

internal sealed record CatalogRouteDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("num")] string Num,
    [property: JsonPropertyName("name")] string Name);

internal sealed record CatalogStopDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("lat")] double Lat,
    [property: JsonPropertyName("lon")] double Lon);

[JsonSerializable(typeof(CatalogDto))]
internal sealed partial class CatalogJsonContext : JsonSerializerContext;