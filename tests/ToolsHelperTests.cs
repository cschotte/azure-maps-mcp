using Azure.Maps.Mcp.Common;
using FluentAssertions;
using Xunit;

namespace Azure.Maps.Mcp.Tests;

public class ToolsHelperTests
{
    [Theory]
    [InlineData("car", true, "car")]
    [InlineData("bicycle", true, "bicycle")]
    [InlineData("hoverboard", false, null)]
    public void ParseTravelMode_Works(string input, bool expectedOk, string? expectedName)
    {
        var result = ToolsHelper.ParseTravelMode(input);
        result.IsValid.Should().Be(expectedOk);
        if (expectedOk) result.Value.ToString().Should().Be(expectedName);
    }

    [Theory]
    [InlineData("fastest", true, "fastest")]
    [InlineData("shortest", true, "shortest")]
    [InlineData("scenic", false, null)]
    public void ParseRouteType_Works(string input, bool expectedOk, string? expectedName)
    {
        var result = ToolsHelper.ParseRouteType(input);
        result.IsValid.Should().Be(expectedOk);
        if (expectedOk) result.Value.ToString().Should().Be(expectedName);
    }
}
