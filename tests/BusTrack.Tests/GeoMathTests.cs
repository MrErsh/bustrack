using BusTrack.Core;
using FluentAssertions;
using Xunit;

namespace BusTrack.Tests;

public class GeoMathTests
{
    [Fact]
    public void SamePoint_DistanceIsZero()
        => GeoMath.DistanceMeters(59.9, 30.3, 59.9, 30.3).Should().Be(0);

    [Fact]
    public void OneDegreeOfLatitude_IsAbout111Km()
        => GeoMath.DistanceMeters(59.0, 30.0, 60.0, 30.0).Should().BeApproximately(111_195, 5);

    [Fact]
    public void Distance_IsSymmetric()
    {
        var ab = GeoMath.DistanceMeters(59.9, 30.3, 60.0, 30.5);
        var ba = GeoMath.DistanceMeters(60.0, 30.5, 59.9, 30.3);
        ab.Should().Be(ba);
    }
}