using BusTrack.Api;
using FluentAssertions;
using Xunit;

namespace BusTrack.Tests;

public class RouteCatalogTests
{
    [Theory]
    [InlineData("2", "10")]
    [InlineData("10", "65")]
    [InlineData("65", "65К")]
    [InlineData("65А", "65Б")]
    public void NaturalCompare_OrdersNumbersLikeHuman(string a, string b)
        => RouteCatalog.NaturalCompare(a, b).Should().BeNegative();

    [Fact]
    public void NaturalCompare_EqualStrings_Zero()
        => RouteCatalog.NaturalCompare("65К", "65К").Should().Be(0);

    [Fact]
    public void EmbeddedCatalog_LoadsRoutesWithStops()
    {
        var catalog = new RouteCatalog();

        catalog.Routes.Should().NotBeEmpty();
        var route65 = catalog.Find("370");
        route65.Should().NotBeNull("дефолтный маршрут из TrackingSettings должен существовать");
        route65!.Directions.Should().NotBeEmpty();
        route65.Directions[0].Should().NotBeEmpty();
        route65.Directions[0][0].Lat.Should().BeInRange(59.0, 61.0);
    }

    [Fact]
    public void Routes_AreNaturallySorted()
    {
        var catalog = new RouteCatalog();
        var numbers = catalog.Routes.Select(r => r.Number).ToList();

        numbers.Should().BeInAscendingOrder(RouteCatalog.NaturalCompare);
    }
}