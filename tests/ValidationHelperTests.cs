using Azure.Maps.Mcp.Common;
using FluentAssertions;
using Xunit;

namespace Azure.Maps.Mcp.Tests;

public class ValidationHelperTests
{
    [Theory]
    [InlineData(47.6062, -122.3321, true)]
    [InlineData(-90, 180, true)]
    [InlineData(91, 0, false)]
    [InlineData(0, -181, false)]
    public void ValidateCoordinates_Works(double lat, double lon, bool expected)
    {
        var (ok, _) = ValidationHelper.ValidateCoordinates(lat, lon);
        ok.Should().Be(expected);
    }

    [Theory]
    [InlineData("true", true, true)]
    [InlineData("false", true, false)]
    [InlineData("yes", false, false)]
    [InlineData(null, false, false)]
    public void ValidateBooleanString_Works(string? input, bool expectedOk, bool expectedVal)
    {
        var (ok, _, val) = ValidationHelper.ValidateBooleanString(input, "flag");
        ok.Should().Be(expectedOk);
        if (ok) val.Should().Be(expectedVal);
    }
}
