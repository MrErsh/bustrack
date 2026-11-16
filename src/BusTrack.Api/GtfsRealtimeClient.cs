using BusTrack.Model;
using Polly;
using Polly.Retry;
using TransitRealtime;

namespace BusTrack.Api;

public interface IGtfsRealtimeClient
{
    /// <summary>Живые позиции всех автобусов маршрута.</summary>
    Task<IReadOnlyList<BusPosition>> GetVehiclesAsync(string routeId, CancellationToken ct = default);

    /// <summary>Прогнозы прибытия для конкретных бортов.</summary>
    Task<IReadOnlyList<ArrivalPrediction>> GetArrivalsAsync(
        IReadOnlyCollection<string> vehicleIds, CancellationToken ct = default);
}

public sealed class GtfsRealtimeClient : IGtfsRealtimeClient
{
    private const string BaseUrl = "https://transport.orgp.spb.ru/Portal/transport/internalapi/gtfs/realtime";

    private readonly HttpClient _http;
    private readonly ResiliencePipeline<byte[]> _pipeline;

    public GtfsRealtimeClient(HttpClient http)
    {
        _http = http;
        _pipeline = new ResiliencePipelineBuilder<byte[]>()
            .AddRetry(new RetryStrategyOptions<byte[]>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder<byte[]>()
                    .Handle<HttpRequestException>()
                    .Handle<OperationCanceledException>(e => e.InnerException is TimeoutException)
            })
            .Build();
    }

    public async Task<IReadOnlyList<BusPosition>> GetVehiclesAsync(string routeId, CancellationToken ct = default)
    {
        var bytes = await FetchAsync($"{BaseUrl}/vehicle?transports=bus&routeIDs={Uri.EscapeDataString(routeId)}", ct);
        var feed = FeedMessage.Parser.ParseFrom(bytes);

        var result = new List<BusPosition>();
        foreach (var entity in feed.Entity)
        {
            var v = entity.Vehicle;
            if (v?.Position is null)
                continue;

            result.Add(new BusPosition(
                VehicleId: v.Vehicle?.Id ?? string.Empty,
                TripId: string.IsNullOrEmpty(v.Trip?.TripId) ? null : v.Trip.TripId,
                Lat: v.Position.Latitude,
                Lon: v.Position.Longitude,
                Bearing: v.Position.HasBearing ? v.Position.Bearing : 0,
                SpeedMps: v.Position.HasSpeed ? v.Position.Speed : 0,
                FixTime: DateTimeOffset.FromUnixTimeSeconds((long)v.Timestamp)));
        }
        return result;
    }

    public async Task<IReadOnlyList<ArrivalPrediction>> GetArrivalsAsync(
        IReadOnlyCollection<string> vehicleIds, CancellationToken ct = default)
    {
        if (vehicleIds.Count == 0)
            return [];

        var ids = string.Join(",", vehicleIds);
        var bytes = await FetchAsync($"{BaseUrl}/vehicletrips?vehicleIDs={Uri.EscapeDataString(ids)}", ct);
        var feed = FeedMessage.Parser.ParseFrom(bytes);

        var result = new List<ArrivalPrediction>();
        foreach (var entity in feed.Entity)
        {
            var update = entity.TripUpdate;
            if (update is null)
                continue;
            foreach (var stopTime in update.StopTimeUpdate)
            {
                if (stopTime.Arrival is null || stopTime.Arrival.Time <= 0)
                    continue;
                result.Add(new ArrivalPrediction(
                    VehicleId: update.Vehicle?.Id ?? string.Empty,
                    TripId: update.Trip?.TripId ?? string.Empty,
                    StopId: stopTime.StopId,
                    Arrival: DateTimeOffset.FromUnixTimeSeconds(stopTime.Arrival.Time)));
            }
        }
        return result;
    }

    private Task<byte[]> FetchAsync(string url, CancellationToken ct)
        => _pipeline.ExecuteAsync(async token =>
        {
            using var response = await _http.GetAsync(url, token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(token);
        }, ct).AsTask();
}