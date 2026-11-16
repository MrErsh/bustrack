using BusTrack.Core;
using FluentAssertions;
using Xunit;

namespace BusTrack.Tests;

public class StopMatcherTests
{
    [Fact]
    public void Match_PicksNearestStop_AndItsDirection()
    {
        var route = TestData.Route(directions:
        [
            [TestData.Stop("a", 59.90, 30.30), TestData.Stop("b", 59.91, 30.30)],
            [TestData.Stop("c", 59.95, 30.30)]
        ]);
        var bus = TestData.Bus("v1", 59.9101, 30.30);

        var tracked = StopMatcher.Match(bus, route);

        tracked.NearestStop!.Id.Should().Be("b");
        tracked.DirectionIndex.Should().Be(0);
        tracked.DistanceToStopMeters.Should().BeLessThan(20);
    }

    [Fact]
    public void Match_CanPickStopFromSecondDirection()
    {
        var route = TestData.Route(directions:
        [
            [TestData.Stop("a", 59.90, 30.30)],
            [TestData.Stop("c", 59.95, 30.30)]
        ]);
        var bus = TestData.Bus("v1", 59.9499, 30.30);

        var tracked = StopMatcher.Match(bus, route);

        tracked.NearestStop!.Id.Should().Be("c");
        tracked.DirectionIndex.Should().Be(1);
    }

    [Fact]
    public void Match_EmptyRoute_NoStop()
    {
        var tracked = StopMatcher.Match(TestData.Bus("v1"), TestData.Route());

        tracked.NearestStop.Should().BeNull();
        tracked.DirectionIndex.Should().Be(-1);
    }

    [Fact]
    public void Match_DirectionFilter_IgnoresOtherDirection()
    {
        var route = TestData.Route(directions:
        [
            [TestData.Stop("a", 59.90, 30.30)],
            [TestData.Stop("c", 59.95, 30.30)]
        ]);
        // Автобус рядом с остановкой второго направления, но выбрано первое.
        var bus = TestData.Bus("v1", 59.9499, 30.30);

        var tracked = StopMatcher.Match(bus, route, directionIndex: 0);

        tracked.NearestStop!.Id.Should().Be("a");
        tracked.DirectionIndex.Should().Be(0);
    }

    [Fact]
    public void Match_DirectionFilter_PicksStopOfSelectedDirection()
    {
        var route = TestData.Route(directions:
        [
            [TestData.Stop("a", 59.90, 30.30)],
            [TestData.Stop("c", 59.95, 30.30)]
        ]);
        var bus = TestData.Bus("v1", 59.9001, 30.30);

        var tracked = StopMatcher.Match(bus, route, directionIndex: 1);

        tracked.NearestStop!.Id.Should().Be("c");
        tracked.DirectionIndex.Should().Be(1);
    }
}