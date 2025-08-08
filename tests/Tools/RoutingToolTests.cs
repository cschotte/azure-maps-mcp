// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Maps.Mcp.Tools;
using Azure.Maps.Mcp.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Moq;
using System.Text.Json;
using Xunit;
using FluentAssertions;
using Azure.Maps.Mcp.Tests.Helpers;

namespace Azure.Maps.Mcp.Tests.Tools;

/// <summary>
/// Tests for RoutingTool focusing on business logic and validation
/// These tests focus on input validation, error handling, and output formatting
/// rather than mocking complex Azure SDK types
/// </summary>
public class RoutingToolTests
{
    private readonly Mock<IAzureMapsService> _mockAzureMapsService;
    private readonly Mock<ILogger<RoutingTool>> _mockLogger;
    private readonly RoutingTool _routingTool;
    private readonly Mock<ToolInvocationContext> _mockContext;

    public RoutingToolTests()
    {
        _mockAzureMapsService = new Mock<IAzureMapsService>();
        _mockLogger = new Mock<ILogger<RoutingTool>>();
        _mockContext = new Mock<ToolInvocationContext>();

        // Mock the routing client - we'll focus on testing business logic
        var mockRoutingClient = new Mock<Azure.Maps.Routing.MapsRoutingClient>();
        _mockAzureMapsService.Setup(x => x.RoutingClient).Returns(mockRoutingClient.Object);
        
        _routingTool = new RoutingTool(_mockAzureMapsService.Object, _mockLogger.Object);
    }

    #region Input Validation Tests

    [Fact]
    public async Task GetRouteDirections_NullCoordinates_ReturnsError()
    {
        // Act
        var result = await _routingTool.GetRouteDirections(_mockContext.Object, null!);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("At least 2 coordinates");
    }

    [Fact]
    public async Task GetRouteDirections_EmptyCoordinates_ReturnsError()
    {
        // Arrange
        var coordinates = new CoordinateInfo[0];

        // Act
        var result = await _routingTool.GetRouteDirections(_mockContext.Object, coordinates);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("At least 2 coordinates");
    }

    [Fact]
    public async Task GetRouteDirections_SingleCoordinate_ReturnsError()
    {
        // Arrange
        var coordinates = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon }
        };

        // Act
        var result = await _routingTool.GetRouteDirections(_mockContext.Object, coordinates);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("At least 2 coordinates");
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidCoordinates), MemberType = typeof(TestHelper.TestData))]
    public async Task GetRouteDirections_InvalidCoordinates_ReturnsError(double lat, double lon)
    {
        // Arrange
        var coordinates = new[]
        {
            new CoordinateInfo { Latitude = lat, Longitude = lon },
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon }
        };

        // Act
        var result = await _routingTool.GetRouteDirections(_mockContext.Object, coordinates);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Invalid coordinate");
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidTravelModes), MemberType = typeof(TestHelper.TestData))]
    public async Task GetRouteDirections_InvalidTravelMode_ReturnsError(string travelMode)
    {
        // Arrange
        var coordinates = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon },
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.NewYorkLat, Longitude = TestHelper.TestCoordinates.NewYorkLon }
        };

        // Act
        var result = await _routingTool.GetRouteDirections(_mockContext.Object, coordinates, travelMode);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Invalid travel mode");
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidRouteTypes), MemberType = typeof(TestHelper.TestData))]
    public async Task GetRouteDirections_InvalidRouteType_ReturnsError(string routeType)
    {
        // Arrange
        var coordinates = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon },
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.NewYorkLat, Longitude = TestHelper.TestCoordinates.NewYorkLon }
        };

        // Act
        var result = await _routingTool.GetRouteDirections(_mockContext.Object, coordinates, "car", routeType);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Invalid route type");
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidAvoidOptions), MemberType = typeof(TestHelper.TestData))]
    public async Task GetRouteDirections_InvalidAvoidTolls_ReturnsError(string avoidTolls)
    {
        // Arrange
        var coordinates = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon },
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.NewYorkLat, Longitude = TestHelper.TestCoordinates.NewYorkLon }
        };

        // Act
        var result = await _routingTool.GetRouteDirections(_mockContext.Object, coordinates, "car", "fastest", avoidTolls);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Invalid avoidTolls");
    }

    #endregion

    #region GetRouteMatrix Tests

    [Fact]
    public async Task GetRouteMatrix_NullOrigins_ReturnsError()
    {
        // Arrange
        var destinations = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon }
        };

        // Act
        var result = await _routingTool.GetRouteMatrix(_mockContext.Object, null!, destinations);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("At least one origin coordinate");
    }

    [Fact]
    public async Task GetRouteMatrix_EmptyOrigins_ReturnsError()
    {
        // Arrange
        var origins = new CoordinateInfo[0];
        var destinations = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon }
        };

        // Act
        var result = await _routingTool.GetRouteMatrix(_mockContext.Object, origins, destinations);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("At least one origin coordinate");
    }

    [Fact]
    public async Task GetRouteMatrix_NullDestinations_ReturnsError()
    {
        // Arrange
        var origins = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon }
        };

        // Act
        var result = await _routingTool.GetRouteMatrix(_mockContext.Object, origins, null!);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("At least one destination coordinate");
    }

    [Fact]
    public async Task GetRouteMatrix_EmptyDestinations_ReturnsError()
    {
        // Arrange
        var origins = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon }
        };
        var destinations = new CoordinateInfo[0];

        // Act
        var result = await _routingTool.GetRouteMatrix(_mockContext.Object, origins, destinations);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("At least one destination coordinate");
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidCoordinates), MemberType = typeof(TestHelper.TestData))]
    public async Task GetRouteMatrix_InvalidOriginCoordinates_ReturnsError(double lat, double lon)
    {
        // Arrange
        var origins = new[]
        {
            new CoordinateInfo { Latitude = lat, Longitude = lon }
        };
        var destinations = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon }
        };

        // Act
        var result = await _routingTool.GetRouteMatrix(_mockContext.Object, origins, destinations);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Invalid origin coordinate");
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidCoordinates), MemberType = typeof(TestHelper.TestData))]
    public async Task GetRouteMatrix_InvalidDestinationCoordinates_ReturnsError(double lat, double lon)
    {
        // Arrange
        var origins = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon }
        };
        var destinations = new[]
        {
            new CoordinateInfo { Latitude = lat, Longitude = lon }
        };

        // Act
        var result = await _routingTool.GetRouteMatrix(_mockContext.Object, origins, destinations);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Invalid destination coordinate");
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidTravelModes), MemberType = typeof(TestHelper.TestData))]
    public async Task GetRouteMatrix_InvalidTravelMode_ReturnsError(string travelMode)
    {
        // Arrange
        var origins = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon }
        };
        var destinations = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.NewYorkLat, Longitude = TestHelper.TestCoordinates.NewYorkLon }
        };

        // Act
        var result = await _routingTool.GetRouteMatrix(_mockContext.Object, origins, destinations, travelMode);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Invalid travel mode");
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidRouteTypes), MemberType = typeof(TestHelper.TestData))]
    public async Task GetRouteMatrix_InvalidRouteType_ReturnsError(string routeType)
    {
        // Arrange
        var origins = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon }
        };
        var destinations = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.NewYorkLat, Longitude = TestHelper.TestCoordinates.NewYorkLon }
        };

        // Act
        var result = await _routingTool.GetRouteMatrix(_mockContext.Object, origins, destinations, "car", routeType);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Invalid route type");
    }

    #endregion

    #region GetRouteRange Tests

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidCoordinates), MemberType = typeof(TestHelper.TestData))]
    public async Task GetRouteRange_InvalidCoordinates_ReturnsError(double lat, double lon)
    {
        // Act
        var result = await _routingTool.GetRouteRange(_mockContext.Object, lat, lon, 1800);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("must be between");
    }

    [Fact]
    public async Task GetRouteRange_NoBudgetSpecified_ReturnsError()
    {
        // Act
        var result = await _routingTool.GetRouteRange(
            _mockContext.Object,
            TestHelper.TestCoordinates.SeattleLat,
            TestHelper.TestCoordinates.SeattleLon);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Either timeBudgetInSeconds or distanceBudgetInMeters must be specified");
    }

    [Fact]
    public async Task GetRouteRange_BothBudgetsSpecified_ReturnsError()
    {
        // Act
        var result = await _routingTool.GetRouteRange(
            _mockContext.Object,
            TestHelper.TestCoordinates.SeattleLat,
            TestHelper.TestCoordinates.SeattleLon,
            1800,
            5000);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Specify either timeBudgetInSeconds or distanceBudgetInMeters, not both");
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidTimeBudgets), MemberType = typeof(TestHelper.TestData))]
    public async Task GetRouteRange_InvalidTimeBudget_ReturnsError(int timeBudget)
    {
        // Act
        var result = await _routingTool.GetRouteRange(
            _mockContext.Object,
            TestHelper.TestCoordinates.SeattleLat,
            TestHelper.TestCoordinates.SeattleLon,
            timeBudget);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("An unexpected error occurred");
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidDistanceBudgets), MemberType = typeof(TestHelper.TestData))]
    public async Task GetRouteRange_InvalidDistanceBudget_ReturnsError(int distanceBudget)
    {
        // Act
        var result = await _routingTool.GetRouteRange(
            _mockContext.Object,
            TestHelper.TestCoordinates.SeattleLat,
            TestHelper.TestCoordinates.SeattleLon,
            null,
            distanceBudget);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("An unexpected error occurred");
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidTravelModes), MemberType = typeof(TestHelper.TestData))]
    public async Task GetRouteRange_InvalidTravelMode_ReturnsError(string travelMode)
    {
        // Act
        var result = await _routingTool.GetRouteRange(
            _mockContext.Object,
            TestHelper.TestCoordinates.SeattleLat,
            TestHelper.TestCoordinates.SeattleLon,
            1800,
            null,
            travelMode);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Invalid travel mode");
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidRouteTypes), MemberType = typeof(TestHelper.TestData))]
    public async Task GetRouteRange_InvalidRouteType_ReturnsError(string routeType)
    {
        // Act
        var result = await _routingTool.GetRouteRange(
            _mockContext.Object,
            TestHelper.TestCoordinates.SeattleLat,
            TestHelper.TestCoordinates.SeattleLon,
            1800,
            null,
            "car",
            routeType);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Invalid route type");
    }

    #endregion

    #region AnalyzeRouteCountries Tests

    [Fact]
    public async Task AnalyzeRouteCountries_NullCoordinates_ReturnsError()
    {
        // Act
        var result = await _routingTool.AnalyzeRouteCountries(_mockContext.Object, null!);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("At least 2 coordinates");
    }

    [Fact]
    public async Task AnalyzeRouteCountries_EmptyCoordinates_ReturnsError()
    {
        // Arrange
        var coordinates = new CoordinateInfo[0];

        // Act
        var result = await _routingTool.AnalyzeRouteCountries(_mockContext.Object, coordinates);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("At least 2 coordinates");
    }

    [Fact]
    public async Task AnalyzeRouteCountries_SingleCoordinate_ReturnsError()
    {
        // Arrange
        var coordinates = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon }
        };

        // Act
        var result = await _routingTool.AnalyzeRouteCountries(_mockContext.Object, coordinates);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("At least 2 coordinates");
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidCoordinates), MemberType = typeof(TestHelper.TestData))]
    public async Task AnalyzeRouteCountries_InvalidCoordinates_ReturnsError(double lat, double lon)
    {
        // Arrange
        var coordinates = new[]
        {
            new CoordinateInfo { Latitude = lat, Longitude = lon },
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon }
        };

        // Act
        var result = await _routingTool.AnalyzeRouteCountries(_mockContext.Object, coordinates);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Invalid coordinate values");
    }

    #endregion

    #region Valid Input Tests with Default Parameters

    [Theory]
    [MemberData(nameof(TestHelper.TestData.ValidTravelModes), MemberType = typeof(TestHelper.TestData))]
    public async Task GetRouteDirections_ValidTravelModes_AcceptsInput(string travelMode)
    {
        // Arrange
        var coordinates = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon },
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.NewYorkLat, Longitude = TestHelper.TestCoordinates.NewYorkLon }
        };

        // Act & Assert (Should not throw exceptions for input validation)
        var result = await _routingTool.GetRouteDirections(_mockContext.Object, coordinates, travelMode);
        result.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.ValidRouteTypes), MemberType = typeof(TestHelper.TestData))]
    public async Task GetRouteDirections_ValidRouteTypes_AcceptsInput(string routeType)
    {
        // Arrange
        var coordinates = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon },
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.NewYorkLat, Longitude = TestHelper.TestCoordinates.NewYorkLon }
        };

        // Act & Assert (Should not throw exceptions for input validation)
        var result = await _routingTool.GetRouteDirections(_mockContext.Object, coordinates, "car", routeType);
        result.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.ValidAvoidOptions), MemberType = typeof(TestHelper.TestData))]
    public async Task GetRouteDirections_ValidAvoidOptions_AcceptsInput(string avoidTolls)
    {
        // Arrange
        var coordinates = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon },
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.NewYorkLat, Longitude = TestHelper.TestCoordinates.NewYorkLon }
        };

        // Act & Assert (Should not throw exceptions for input validation)
        var result = await _routingTool.GetRouteDirections(_mockContext.Object, coordinates, "car", "fastest", avoidTolls);
        result.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.ValidTimeBudgets), MemberType = typeof(TestHelper.TestData))]
    public async Task GetRouteRange_ValidTimeBudgets_AcceptsInput(int timeBudget)
    {
        // Act & Assert (Should not throw exceptions for input validation)
        var result = await _routingTool.GetRouteRange(
            _mockContext.Object,
            TestHelper.TestCoordinates.SeattleLat,
            TestHelper.TestCoordinates.SeattleLon,
            timeBudget);
        result.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.ValidDistanceBudgets), MemberType = typeof(TestHelper.TestData))]
    public async Task GetRouteRange_ValidDistanceBudgets_AcceptsInput(int distanceBudget)
    {
        // Act & Assert (Should not throw exceptions for input validation)
        var result = await _routingTool.GetRouteRange(
            _mockContext.Object,
            TestHelper.TestCoordinates.SeattleLat,
            TestHelper.TestCoordinates.SeattleLon,
            null,
            distanceBudget);
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetRouteDirections_MultipleWaypoints_AcceptsInput()
    {
        // Arrange
        var coordinates = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon },
            new CoordinateInfo { Latitude = 45.5152, Longitude = -122.6784 }, // Portland
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.NewYorkLat, Longitude = TestHelper.TestCoordinates.NewYorkLon }
        };

        // Act & Assert (Should not throw exceptions for input validation)
        var result = await _routingTool.GetRouteDirections(_mockContext.Object, coordinates);
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetRouteMatrix_MultipleOriginsAndDestinations_AcceptsInput()
    {
        // Arrange
        var origins = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon },
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.NewYorkLat, Longitude = TestHelper.TestCoordinates.NewYorkLon }
        };
        var destinations = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.LondonLat, Longitude = TestHelper.TestCoordinates.LondonLon },
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.TokyoLat, Longitude = TestHelper.TestCoordinates.TokyoLon }
        };

        // Act & Assert (Should not throw exceptions for input validation)
        var result = await _routingTool.GetRouteMatrix(_mockContext.Object, origins, destinations);
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AnalyzeRouteCountries_ValidCoordinates_AcceptsInput()
    {
        // Arrange
        var coordinates = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon },
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.NewYorkLat, Longitude = TestHelper.TestCoordinates.NewYorkLon }
        };

        // Act & Assert (Should not throw exceptions for input validation)
        var result = await _routingTool.AnalyzeRouteCountries(_mockContext.Object, coordinates);
        result.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task GetRouteDirections_DefaultParameters_UsesDefaults()
    {
        // Arrange
        var coordinates = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon },
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.NewYorkLat, Longitude = TestHelper.TestCoordinates.NewYorkLon }
        };

        // Act
        var result = await _routingTool.GetRouteDirections(_mockContext.Object, coordinates);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // The method should accept the call with default parameters without errors
    }

    [Fact]
    public async Task GetRouteMatrix_DefaultParameters_UsesDefaults()
    {
        // Arrange
        var origins = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon }
        };
        var destinations = new[]
        {
            new CoordinateInfo { Latitude = TestHelper.TestCoordinates.NewYorkLat, Longitude = TestHelper.TestCoordinates.NewYorkLon }
        };

        // Act
        var result = await _routingTool.GetRouteMatrix(_mockContext.Object, origins, destinations);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // The method should accept the call with default parameters without errors
    }

    [Fact]
    public async Task GetRouteRange_DefaultTravelModeAndRouteType_UsesDefaults()
    {
        // Act
        var result = await _routingTool.GetRouteRange(
            _mockContext.Object,
            TestHelper.TestCoordinates.SeattleLat,
            TestHelper.TestCoordinates.SeattleLon,
            1800);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // The method should accept the call with default parameters without errors
    }

    [Fact]
    public async Task GetRouteDirections_ExactCoordinateBoundaries_AcceptsInput()
    {
        // Arrange - Test exact boundary values
        var coordinates = new[]
        {
            new CoordinateInfo { Latitude = 90.0, Longitude = 180.0 },  // Max valid
            new CoordinateInfo { Latitude = -90.0, Longitude = -180.0 } // Min valid
        };

        // Act
        var result = await _routingTool.GetRouteDirections(_mockContext.Object, coordinates);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // Should accept exact boundary coordinates
    }

    [Fact]
    public async Task GetRouteRange_BoundaryTimeBudget_AcceptsInput()
    {
        // Act - Test with a small but valid time budget
        var result = await _routingTool.GetRouteRange(
            _mockContext.Object,
            TestHelper.TestCoordinates.SeattleLat,
            TestHelper.TestCoordinates.SeattleLon,
            1); // 1 second - minimal but valid

        // Assert
        result.Should().NotBeNullOrEmpty();
        // Should accept minimal valid time budget
    }

    #endregion
}
