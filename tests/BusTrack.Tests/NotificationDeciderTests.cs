using BusTrack.Core;
using BusTrack.Model;
using FluentAssertions;
using Xunit;

namespace BusTrack.Tests;

public class NotificationDeciderTests
{
    private readonly NotificationDecider _decider = new();
    private readonly BusRoute _route = TestData.Route(directions: [[TestData.Stop("s1")]]);
    private readonly TrackingSettings _settings = new("370", "s1", NotifyBeforeMinutes: 5);

    [Fact]
    public void NoWatchedStop_NoNotifications()
    {
        var predictions = new[] { TestData.Arrival("v1", "s1", TestData.T0.AddMinutes(3)) };

        _decider.Decide(predictions, _settings with { WatchedStopId = null }, _route, TestData.T0)
            .Should().BeEmpty();
    }

    [Fact]
    public void ArrivalWithinHorizon_NotifiesOnce_WithStopName()
    {
        var arrival = TestData.T0.AddMinutes(3);
        var predictions = new[] { TestData.Arrival("v1", "s1", arrival) };

        var first = _decider.Decide(predictions, _settings, _route, TestData.T0);
        var second = _decider.Decide(predictions, _settings, _route, TestData.T0.AddSeconds(30));

        first.Should().ContainSingle();
        first[0].StopName.Should().Be("Остановка s1");
        first[0].Arrival.Should().Be(arrival);
        second.Should().BeEmpty("один рейс — одно уведомление");
    }

    [Fact]
    public void ArrivalBeyondHorizon_NoNotification()
    {
        var predictions = new[] { TestData.Arrival("v1", "s1", TestData.T0.AddMinutes(6)) };

        _decider.Decide(predictions, _settings, _route, TestData.T0).Should().BeEmpty();
    }

    [Fact]
    public void PastArrival_NoNotification()
    {
        var predictions = new[] { TestData.Arrival("v1", "s1", TestData.T0.AddSeconds(-1)) };

        _decider.Decide(predictions, _settings, _route, TestData.T0).Should().BeEmpty();
    }

    [Fact]
    public void OtherStop_NoNotification()
    {
        var predictions = new[] { TestData.Arrival("v1", "s2", TestData.T0.AddMinutes(3)) };

        _decider.Decide(predictions, _settings, _route, TestData.T0).Should().BeEmpty();
    }

    [Fact]
    public void DifferentTrip_NotifiesAgain()
    {
        var first = new[] { TestData.Arrival("v1", "s1", TestData.T0.AddMinutes(3), tripId: "t1") };
        var second = new[] { TestData.Arrival("v1", "s1", TestData.T0.AddMinutes(4), tripId: "t2") };

        _decider.Decide(first, _settings, _route, TestData.T0).Should().ContainSingle();
        _decider.Decide(second, _settings, _route, TestData.T0).Should().ContainSingle();
    }

    [Fact]
    public void Reset_AllowsSameTripAgain()
    {
        var predictions = new[] { TestData.Arrival("v1", "s1", TestData.T0.AddMinutes(3)) };

        _decider.Decide(predictions, _settings, _route, TestData.T0).Should().ContainSingle();
        _decider.Reset();
        _decider.Decide(predictions, _settings, _route, TestData.T0).Should().ContainSingle();
    }

    [Fact]
    public void OldEntriesPruned_AfterHourSameKeyNotifiesAgain()
    {
        var predictions = new[] { TestData.Arrival("v1", "s1", TestData.T0.AddMinutes(3)) };
        _decider.Decide(predictions, _settings, _route, TestData.T0).Should().ContainSingle();

        // Спустя 65 минут после прибытия старая запись вычищена (PruneAge = 1 час от времени прибытия).
        var later = TestData.T0.AddMinutes(65);
        var again = new[] { TestData.Arrival("v1", "s1", later.AddMinutes(3)) };
        _decider.Decide(again, _settings, _route, later).Should().ContainSingle();
    }

    [Fact]
    public void WatchedStopInSelectedDirection_Notifies()
    {
        // Остановки с одинаковыми названиями, но разными ID в разных направлениях.
        var route = TestData.Route(directions:
        [
            [new Stop("s1", "Двинская ул.", 59.9, 30.3)],
            [new Stop("s2", "Двинская ул.", 59.9, 30.31)]
        ]);
        var settings = new TrackingSettings("370", "s2", 5, DirectionIndex: 1);
        var predictions = new[] { TestData.Arrival("v1", "s2", TestData.T0.AddMinutes(3)) };

        var notifications = _decider.Decide(predictions, settings, route, TestData.T0);

        notifications.Should().ContainSingle();
        notifications[0].StopName.Should().Be("Двинская ул.");
    }

    [Fact]
    public void WatchedStopOfOtherDirection_NoNotification()
    {
        var route = TestData.Route(directions:
        [
            [new Stop("s1", "Двинская ул.", 59.9, 30.3)],
            [new Stop("s2", "Двинская ул.", 59.9, 30.31)]
        ]);
        // Остановка второго направления, но выбрано первое.
        var settings = new TrackingSettings("370", "s2", 5, DirectionIndex: 0);
        var predictions = new[] { TestData.Arrival("v1", "s2", TestData.T0.AddMinutes(3)) };

        _decider.Decide(predictions, settings, route, TestData.T0).Should().BeEmpty();
    }
}