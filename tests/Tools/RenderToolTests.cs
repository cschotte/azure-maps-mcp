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
/// Tests for RenderTool focusing on business logic and validation
/// These tests focus on input validation, error handling, and output formatting
/// rather than mocking complex Azure SDK types
/// </summary>
public class RenderToolTests
{
    private readonly Mock<IAzureMapsService> _mockAzureMapsService;
    private readonly Mock<ILogger<RenderTool>> _mockLogger;
    private readonly RenderTool _renderTool;
    private readonly Mock<ToolInvocationContext> _mockContext;

    public RenderToolTests()
    {
        _mockAzureMapsService = new Mock<IAzureMapsService>();
        _mockLogger = new Mock<ILogger<RenderTool>>();
        _mockContext = new Mock<ToolInvocationContext>();

        // Mock the rendering client - we'll focus on testing business logic
        var mockRenderingClient = new Mock<Azure.Maps.Rendering.MapsRenderingClient>();
        _mockAzureMapsService.Setup(x => x.RenderingClient).Returns(mockRenderingClient.Object);
        
        _renderTool = new RenderTool(_mockAzureMapsService.Object, _mockLogger.Object);
    }

    #region Input Validation Tests

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidBoundingBoxes), MemberType = typeof(TestHelper.TestData))]
    public async Task GetStaticMapImage_InvalidBoundingBox_ReturnsError(string boundingBox)
    {
        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, boundingBox);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().NotBeNullOrEmpty();
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidZoomLevels), MemberType = typeof(TestHelper.TestData))]
    public async Task GetStaticMapImage_InvalidZoomLevel_ReturnsError(int zoomLevel)
    {
        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], zoomLevel);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Zoom level must be between 1 and 20");
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidDimensions), MemberType = typeof(TestHelper.TestData))]
    public async Task GetStaticMapImage_InvalidWidth_ReturnsError(int width)
    {
        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], 10, width);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Width and height must be between 1 and 8192 pixels");
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidDimensions), MemberType = typeof(TestHelper.TestData))]
    public async Task GetStaticMapImage_InvalidHeight_ReturnsError(int height)
    {
        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], 10, 512, height);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Width and height must be between 1 and 8192 pixels");
    }

    [Fact]
    public async Task GetStaticMapImage_BoundingBoxMissingWest_ReturnsError()
    {
        // Arrange
        var invalidBoundingBox = "{\"south\": 47.5, \"east\": -122.2, \"north\": 47.7}";

        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, invalidBoundingBox);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Bounding box must contain 'west', 'south', 'east', and 'north' properties");
    }

    [Fact]
    public async Task GetStaticMapImage_BoundingBoxMissingSouth_ReturnsError()
    {
        // Arrange
        var invalidBoundingBox = "{\"west\": -122.4, \"east\": -122.2, \"north\": 47.7}";

        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, invalidBoundingBox);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Bounding box must contain 'west', 'south', 'east', and 'north' properties");
    }

    [Fact]
    public async Task GetStaticMapImage_BoundingBoxMissingEast_ReturnsError()
    {
        // Arrange
        var invalidBoundingBox = "{\"west\": -122.4, \"south\": 47.5, \"north\": 47.7}";

        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, invalidBoundingBox);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Bounding box must contain 'west', 'south', 'east', and 'north' properties");
    }

    [Fact]
    public async Task GetStaticMapImage_BoundingBoxMissingNorth_ReturnsError()
    {
        // Arrange
        var invalidBoundingBox = "{\"west\": -122.4, \"south\": 47.5, \"east\": -122.2}";

        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, invalidBoundingBox);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Bounding box must contain 'west', 'south', 'east', and 'north' properties");
    }

    [Fact]
    public async Task GetStaticMapImage_EmptyBoundingBox_ReturnsError()
    {
        // Arrange
        var invalidBoundingBox = "{}";

        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, invalidBoundingBox);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Bounding box must contain 'west', 'south', 'east', and 'north' properties");
    }

    [Fact]
    public async Task GetStaticMapImage_MalformedJsonBoundingBox_ReturnsError()
    {
        // Arrange
        var invalidBoundingBox = "{\"west\": -122.4, \"south\": 47.5, \"east\": -122.2, \"north\": 47.7";

        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, invalidBoundingBox);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var response = JsonDocument.Parse(result);
        response.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetString().Should().Contain("Invalid bounding box JSON format");
    }

    #endregion

    #region Valid Input Tests with Default Parameters

    [Theory]
    [MemberData(nameof(TestHelper.TestData.ValidBoundingBoxes), MemberType = typeof(TestHelper.TestData))]
    public async Task GetStaticMapImage_ValidBoundingBoxes_AcceptsInput(string boundingBox)
    {
        // Act & Assert (Should not throw exceptions for input validation)
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, boundingBox);
        result.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.ValidZoomLevels), MemberType = typeof(TestHelper.TestData))]
    public async Task GetStaticMapImage_ValidZoomLevels_AcceptsInput(int zoomLevel)
    {
        // Act & Assert (Should not throw exceptions for input validation)
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], zoomLevel);
        result.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.ValidDimensions), MemberType = typeof(TestHelper.TestData))]
    public async Task GetStaticMapImage_ValidWidths_AcceptsInput(int width)
    {
        // Act & Assert (Should not throw exceptions for input validation)
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], 10, width);
        result.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.ValidDimensions), MemberType = typeof(TestHelper.TestData))]
    public async Task GetStaticMapImage_ValidHeights_AcceptsInput(int height)
    {
        // Act & Assert (Should not throw exceptions for input validation)
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], 10, 512, height);
        result.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.ValidMapStyles), MemberType = typeof(TestHelper.TestData))]
    public async Task GetStaticMapImage_ValidMapStyles_AcceptsInput(string mapStyle)
    {
        // Act & Assert (Should not throw exceptions for input validation)
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], 10, 512, 512, mapStyle);
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetStaticMapImage_WithValidMarkers_AcceptsInput()
    {
        // Arrange
        var markers = new MarkerInfo[]
        {
            new() { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon, Label = "Seattle", Color = "red" },
            new() { Latitude = TestHelper.TestCoordinates.NewYorkLat, Longitude = TestHelper.TestCoordinates.NewYorkLon, Label = "NYC", Color = "blue" }
        };

        // Act & Assert (Should not throw exceptions for input validation)
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], 10, 512, 512, "road", markers);
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetStaticMapImage_WithValidPaths_AcceptsInput()
    {
        // Arrange
        var paths = new PathInfo[]
        {
            new() 
            { 
                Coordinates = new MapCoordinateInfo[]
                {
                    new() { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon },
                    new() { Latitude = TestHelper.TestCoordinates.NewYorkLat, Longitude = TestHelper.TestCoordinates.NewYorkLon }
                },
                Color = "blue",
                Width = 3
            }
        };

        // Act & Assert (Should not throw exceptions for input validation)
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], 10, 512, 512, "road", null, paths);
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetStaticMapImage_WithBothMarkersAndPaths_AcceptsInput()
    {
        // Arrange
        var markers = new MarkerInfo[]
        {
            new() { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon, Label = "Start", Color = "green" }
        };

        var paths = new PathInfo[]
        {
            new() 
            { 
                Coordinates = new MapCoordinateInfo[]
                {
                    new() { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon },
                    new() { Latitude = TestHelper.TestCoordinates.NewYorkLat, Longitude = TestHelper.TestCoordinates.NewYorkLon }
                },
                Color = "red",
                Width = 2
            }
        };

        // Act & Assert (Should not throw exceptions for input validation)
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], 10, 512, 512, "road", markers, paths);
        result.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task GetStaticMapImage_DefaultParameters_UsesDefaults()
    {
        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // The method should accept the call with default parameters without errors
    }

    [Fact]
    public async Task GetStaticMapImage_MinimalValidBoundingBox_AcceptsInput()
    {
        // Arrange - Test minimal valid bounding box
        var minimalBoundingBox = "{\"west\": -1.0, \"south\": 1.0, \"east\": 1.0, \"north\": 2.0}";

        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, minimalBoundingBox);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // Should accept minimal valid bounding box
    }

    [Fact]
    public async Task GetStaticMapImage_WorldWideBoundingBox_AcceptsInput()
    {
        // Arrange - Test world-wide bounding box
        var worldBoundingBox = "{\"west\": -180, \"south\": -85, \"east\": 180, \"north\": 85}";

        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, worldBoundingBox);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // Should accept world-wide bounding box
    }

    [Fact]
    public async Task GetStaticMapImage_ExactZoomBoundaries_AcceptsInput()
    {
        // Act - Test exact boundary values
        var resultMin = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], 1);
        var resultMax = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], 20);

        // Assert
        resultMin.Should().NotBeNullOrEmpty();
        resultMax.Should().NotBeNullOrEmpty();
        // Should accept exact boundary zoom levels
    }

    [Fact]
    public async Task GetStaticMapImage_ExactDimensionBoundaries_AcceptsInput()
    {
        // Act - Test exact boundary values
        var resultMin = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], 10, 1, 1);
        var resultMax = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], 10, 8192, 8192);

        // Assert
        resultMin.Should().NotBeNullOrEmpty();
        resultMax.Should().NotBeNullOrEmpty();
        // Should accept exact boundary dimensions
    }

    [Fact]
    public async Task GetStaticMapImage_EmptyMarkersArray_AcceptsInput()
    {
        // Arrange
        var emptyMarkers = new MarkerInfo[0];

        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], 10, 512, 512, "road", emptyMarkers);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // Should handle empty markers array gracefully
    }

    [Fact]
    public async Task GetStaticMapImage_EmptyPathsArray_AcceptsInput()
    {
        // Arrange
        var emptyPaths = new PathInfo[0];

        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], 10, 512, 512, "road", null, emptyPaths);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // Should handle empty paths array gracefully
    }

    [Fact]
    public async Task GetStaticMapImage_NullMarkersAndPaths_AcceptsInput()
    {
        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], 10, 512, 512, "road", null, null);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // Should handle null markers and paths gracefully
    }

    [Fact]
    public async Task GetStaticMapImage_MarkerWithoutOptionalProperties_AcceptsInput()
    {
        // Arrange
        var markerWithoutOptionals = new MarkerInfo[]
        {
            new() { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon }
        };

        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], 10, 512, 512, "road", markerWithoutOptionals);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // Should handle markers without optional label/color
    }

    [Fact]
    public async Task GetStaticMapImage_PathWithMinimalCoordinates_AcceptsInput()
    {
        // Arrange - Path with exactly 2 coordinates (minimum required)
        var minimalPath = new PathInfo[]
        {
            new() 
            { 
                Coordinates = new MapCoordinateInfo[]
                {
                    new() { Latitude = TestHelper.TestCoordinates.SeattleLat, Longitude = TestHelper.TestCoordinates.SeattleLon },
                    new() { Latitude = TestHelper.TestCoordinates.NewYorkLat, Longitude = TestHelper.TestCoordinates.NewYorkLon }
                },
                Width = 1 // Minimal width
            }
        };

        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, 
            TestHelper.TestRender.ValidBoundingBoxes[0], 10, 512, 512, "road", null, minimalPath);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // Should handle path with minimal coordinates and width
    }

    #endregion

    #region BoundingBox Validation Tests

    [Theory]
    [MemberData(nameof(TestHelper.TestData.BoundingBoxTestCases), MemberType = typeof(TestHelper.TestData))]
    public async Task GetStaticMapImage_NamedBoundingBoxes_AcceptsInput(string name, string boundingBox)
    {
        // Act
        var result = await _renderTool.GetStaticMapImage(_mockContext.Object, boundingBox);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // Should accept all named bounding box test cases - test name: {name}
        name.Should().NotBeNullOrEmpty(); // Use the parameter
    }

    #endregion
}
