using BusTrack.Api;
using BusTrack.Core;
using BusTrack.Model;
using FluentAssertions;
using Moq;
using Xunit;

namespace BusTrack.Tests;

public class TrackingServiceTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly Mock<IGtfsRealtimeClient> _api = new();
    private readonly Mock<IRouteCatalog> _catalog = new();
    private readonly BusRoute _route = TestData.Route(directions: [[TestData.Stop("s1")]]);

    private TrackingService CreateService()
    {
        _catalog.Setup(c => c.Find(It.IsAny<string>())).Returns(_route);
        var engine = new TrackingEngine(_api.Object, _catalog.Object);
        return new TrackingService(engine, Interval);
    }

    private static Task<TrackingSnapshot> NextSnapshot(TrackingService service)
    {
        var tcs = new TaskCompletionSource<TrackingSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(TrackingSnapshot s)
        {
            service.SnapshotReceived -= Handler;
            tcs.TrySetResult(s);
        }
        service.SnapshotReceived += Handler;
        return tcs.Task.WaitAsync(Timeout);
    }

    [Fact]
    public async Task Start_PollsUntilStopped()
    {
        _api.Setup(a => a.GetVehiclesAsync("370", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { TestData.Bus("v1", fixTime: DateTimeOffset.UtcNow) });
        using var service = CreateService();

        service.IsRunning.Should().BeFalse();
        var pending = NextSnapshot(service);
        service.Start(new TrackingSettings("370", null, 5));

        var snapshot = await pending;
        snapshot.Buses.Should().ContainSingle();
        service.IsRunning.Should().BeTrue();

        service.Stop();
        service.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task ApiErrorAfterSuccess_KeepsLastKnownBuses()
    {
        _api.SetupSequence(a => a.GetVehiclesAsync("370", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { TestData.Bus("v1", fixTime: DateTimeOffset.UtcNow) })
            .ThrowsAsync(new HttpRequestException("обрыв"));
        using var service = CreateService();

        var first = NextSnapshot(service);
        service.Start(new TrackingSettings("370", null, 5));
        (await first).Error.Should().BeNull();

        var second = NextSnapshot(service);
        var errored = await second;
        errored.Error.Should().Be("обрыв");
        errored.Buses.Should().ContainSingle("последние известные позиции сохраняются");
        service.Stop();
    }

    [Fact]
    public async Task UpdateSettings_WhileRunning_SwitchesRoute()
    {
        _api.Setup(a => a.GetVehiclesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BusPosition>());
        using var service = CreateService();

        service.Start(new TrackingSettings("370", null, 5));
        await NextSnapshot(service);

        var switched = NextSnapshot(service);
        service.UpdateSettings(new TrackingSettings("400", null, 5));
        await switched;

        _api.Verify(a => a.GetVehiclesAsync("400", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        service.Settings!.RouteId.Should().Be("400");
        service.Stop();
    }
}