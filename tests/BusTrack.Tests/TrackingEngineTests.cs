using BusTrack.Api;
using BusTrack.Core;
using BusTrack.Model;
using FluentAssertions;
using Moq;
using Xunit;

namespace BusTrack.Tests;

public class TrackingEngineTests
{
    private readonly Mock<IGtfsRealtimeClient> _api = new();
    private readonly Mock<IRouteCatalog> _catalog = new();
    private readonly FakeTimeProvider _time = new(TestData.T0);
    private readonly TrackingEngine _engine;

    private readonly BusRoute _route = TestData.Route(directions:
    [
        [TestData.Stop("s1", 59.90, 30.30), TestData.Stop("s2", 59.92, 30.30)]
    ]);

    public TrackingEngineTests()
    {
        _catalog.Setup(c => c.Find("370")).Returns(_route);
        _engine = new TrackingEngine(_api.Object, _catalog.Object, _time);
    }

    [Fact]
    public async Task UnknownRoute_ReturnsError_WithoutApiCall()
    {
        var snapshot = await _engine.PollAsync(new TrackingSettings("999", null, 5));

        snapshot.Error.Should().Contain("999");
        snapshot.Buses.Should().BeEmpty();
        _api.Verify(a => a.GetVehiclesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FreshBuses_MatchedAndSortedByDistance_StaleDropped()
    {
        var near = TestData.Bus("near", 59.90001, 30.30);
        var far = TestData.Bus("far", 59.919, 30.30);
        var stale = TestData.Bus("stale", 59.90, 30.30, TestData.T0.AddMinutes(-4));
        _api.Setup(a => a.GetVehiclesAsync("370", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { far, stale, near });

        var snapshot = await _engine.PollAsync(new TrackingSettings("370", null, 5));

        snapshot.Error.Should().BeNull();
        snapshot.Buses.Select(b => b.Position.VehicleId).Should().Equal("near", "far");
        snapshot.Buses[0].NearestStop!.Id.Should().Be("s1");
        snapshot.Buses[1].NearestStop!.Id.Should().Be("s2");
    }

    [Fact]
    public async Task ApiFailure_ReturnsError()
    {
        _api.Setup(a => a.GetVehiclesAsync("370", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("сеть недоступна"));

        var snapshot = await _engine.PollAsync(new TrackingSettings("370", null, 5));

        snapshot.Error.Should().Be("сеть недоступна");
        snapshot.Buses.Should().BeEmpty();
    }

    [Fact]
    public async Task WatchedStop_FetchesArrivalsForFreshBuses_AndNotifies()
    {
        var bus = TestData.Bus("v1", 59.90, 30.30);
        _api.Setup(a => a.GetVehiclesAsync("370", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { bus });
        _api.Setup(a => a.GetArrivalsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { TestData.Arrival("v1", "s1", TestData.T0.AddMinutes(3)) });

        var snapshot = await _engine.PollAsync(new TrackingSettings("370", "s1", 5));

        snapshot.Notifications.Should().ContainSingle();
        _api.Verify(a => a.GetArrivalsAsync(
            It.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { "v1" })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NoWatchedStop_ArrivalsNotFetched()
    {
        _api.Setup(a => a.GetVehiclesAsync("370", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { TestData.Bus("v1") });

        var snapshot = await _engine.PollAsync(new TrackingSettings("370", null, 5));

        snapshot.Notifications.Should().BeEmpty();
        _api.Verify(a => a.GetArrivalsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _api.Setup(a => a.GetVehiclesAsync("370", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var act = () => _engine.PollAsync(new TrackingSettings("370", null, 5), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}