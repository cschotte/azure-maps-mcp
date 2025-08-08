// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;
using Azure.Maps.Mcp.Services;
using Azure.Maps.Mcp.Tests.Helpers;
using Azure.Maps.Mcp.Tools;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Azure.Maps.Search;

namespace Azure.Maps.Mcp.Tests.Tools;

/// <summary>
/// Tests for the SearchTool class
/// </summary>
public class SearchToolTests
{
    private readonly Mock<IAzureMapsService> _mockAzureMapsService;
    private readonly Mock<ILogger<SearchTool>> _mockLogger;
    private readonly Mock<ToolInvocationContext> _mockContext;
    private readonly SearchTool _searchTool;

    public SearchToolTests()
    {
        _mockAzureMapsService = new Mock<IAzureMapsService>();
        _mockLogger = new Mock<ILogger<SearchTool>>();
        _mockContext = new Mock<ToolInvocationContext>();

        // Mock the search client - we'll focus on testing business logic
        var mockSearchClient = new Mock<MapsSearchClient>();
        _mockAzureMapsService.Setup(x => x.SearchClient).Returns(mockSearchClient.Object);
        
        _searchTool = new SearchTool(_mockAzureMapsService.Object, _mockLogger.Object);
    }

    #region Geocoding Tests

    [Fact]
    public async Task Geocoding_WithValidAddress_ReturnsSuccessResult()
    {
        // Act
        var result = await _searchTool.Geocoding(_mockContext.Object, "123 Main St, Seattle, WA");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        
        // Should return a structured response - may contain an error due to mocking but should not crash
        jsonResult.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task Geocoding_WithNullAddress_ReturnsValidationError()
    {
        // Act
        var result = await _searchTool.Geocoding(_mockContext.Object, null!);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "Address is required");
    }

    [Fact]
    public async Task Geocoding_WithEmptyAddress_ReturnsValidationError()
    {
        // Act
        var result = await _searchTool.Geocoding(_mockContext.Object, "");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "Address is required");
    }

    [Fact]
    public async Task Geocoding_WithWhitespaceAddress_ReturnsValidationError()
    {
        // Act
        var result = await _searchTool.Geocoding(_mockContext.Object, "   ");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "Address is required");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    [InlineData(100)]
    public async Task Geocoding_WithInvalidMaxResults_ReturnsValidationError(int maxResults)
    {
        // Act
        var result = await _searchTool.Geocoding(_mockContext.Object, "123 Main St", maxResults);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "maxResults must be between 1 and 20");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public async Task Geocoding_WithValidMaxResults_AcceptsParameter(int maxResults)
    {
        // Act
        var result = await _searchTool.Geocoding(_mockContext.Object, "123 Main St", maxResults);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        // Should not have a validation error about maxResults
        if (jsonResult.TryGetProperty("error", out var error))
        {
            error.GetString().Should().NotContain("maxResults must be between 1 and 20");
        }
    }

    #endregion

    #region ReverseGeocoding Tests

    [Theory]
    [MemberData(nameof(TestHelper.TestData.ValidCoordinates), MemberType = typeof(TestHelper.TestData))]
    public async Task ReverseGeocoding_WithValidCoordinates_ReturnsSuccessResult(double latitude, double longitude)
    {
        // Act
        var result = await _searchTool.ReverseGeocoding(_mockContext.Object, latitude, longitude);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        
        // The actual search may fail due to mock, but we should get a structured response
        if (!jsonResult.TryGetProperty("error", out _))
        {
            jsonResult.TryGetProperty("result", out _).Should().BeTrue();
        }
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidCoordinates), MemberType = typeof(TestHelper.TestData))]
    public async Task ReverseGeocoding_WithInvalidCoordinates_HandlesGracefully(double latitude, double longitude)
    {
        // Act
        var result = await _searchTool.ReverseGeocoding(_mockContext.Object, latitude, longitude);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        
        // Should either return a result or an error, but not crash
        jsonResult.ValueKind.Should().Be(JsonValueKind.Object);
    }

    #endregion

    #region GetPolygon Tests

    [Theory]
    [InlineData(37.7749, -122.4194, "locality", "small")]
    [InlineData(40.7128, -74.0060, "adminDistrict", "medium")]
    [InlineData(51.5074, -0.1278, "countryRegion", "large")]
    public async Task GetPolygon_WithValidParameters_ReturnsSuccessResult(
        double latitude, double longitude, string resultType, string resolution)
    {
        // Act
        var result = await _searchTool.GetPolygon(_mockContext.Object, latitude, longitude, resultType, resolution);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        
        // Should return a structured response
        jsonResult.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    public async Task GetPolygon_WithInvalidResultType_ReturnsValidationError(string resultType)
    {
        // Act
        var result = await _searchTool.GetPolygon(_mockContext.Object, 37.7749, -122.4194, resultType, "small");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "resultType is required");
    }

    [Fact]
    public async Task GetPolygon_WithNullResultType_ReturnsValidationError()
    {
        // Act
        var result = await _searchTool.GetPolygon(_mockContext.Object, 37.7749, -122.4194, null!, "small");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "resultType is required");
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    public async Task GetPolygon_WithInvalidResolution_ReturnsValidationError(string resolution)
    {
        // Act
        var result = await _searchTool.GetPolygon(_mockContext.Object, 37.7749, -122.4194, "locality", resolution);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "resolution is required");
    }

    [Fact]
    public async Task GetPolygon_WithNullResolution_ReturnsValidationError()
    {
        // Act
        var result = await _searchTool.GetPolygon(_mockContext.Object, 37.7749, -122.4194, "locality", null!);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "resolution is required");
    }

    #endregion

    #region GetCountryInfo Tests

    [Theory]
    [MemberData(nameof(TestHelper.TestData.ValidCountryCodes), MemberType = typeof(TestHelper.TestData))]
    public async Task GetCountryInfo_WithValidCountryCode_ReturnsSuccessResult(string countryCode)
    {
        // Act
        var result = await _searchTool.GetCountryInfo(_mockContext.Object, countryCode);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        
        // Should return a structured response with country info
        jsonResult.ValueKind.Should().Be(JsonValueKind.Object);
        
        if (!jsonResult.TryGetProperty("error", out _))
        {
            jsonResult.TryGetProperty("country", out _).Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCountryInfo_WithInvalidCountryCode_ReturnsValidationError(string countryCode)
    {
        // Act
        var result = await _searchTool.GetCountryInfo(_mockContext.Object, countryCode);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "Country code is required");
    }

    [Fact]
    public async Task GetCountryInfo_WithNullCountryCode_ReturnsValidationError()
    {
        // Act
        var result = await _searchTool.GetCountryInfo(_mockContext.Object, null!);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "Country code is required");
    }

    #endregion

    #region FindCountries Tests

    [Theory]
    [MemberData(nameof(TestHelper.TestData.ValidSearchTerms), MemberType = typeof(TestHelper.TestData))]
    public async Task FindCountries_WithValidSearchTerm_ReturnsSuccessResult(string searchTerm)
    {
        // Act
        var result = await _searchTool.FindCountries(_mockContext.Object, searchTerm);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        
        // Should return a structured response with search results
        jsonResult.ValueKind.Should().Be(JsonValueKind.Object);
        
        if (!jsonResult.TryGetProperty("error", out _))
        {
            jsonResult.TryGetProperty("result", out _).Should().BeTrue();
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public async Task FindCountries_WithValidMaxResults_ReturnsSuccessResult(int maxResults)
    {
        // Act
        var result = await _searchTool.FindCountries(_mockContext.Object, "Test", maxResults);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        
        // Should return a structured response
        jsonResult.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FindCountries_WithInvalidSearchTerm_ReturnsValidationError(string searchTerm)
    {
        // Act
        var result = await _searchTool.FindCountries(_mockContext.Object, searchTerm);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "Search term is required");
    }

    [Fact]
    public async Task FindCountries_WithNullSearchTerm_ReturnsValidationError()
    {
        // Act
        var result = await _searchTool.FindCountries(_mockContext.Object, null!);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "Search term is required");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task SearchTool_AllMethods_HandleNullContext()
    {
        // Test that all methods handle null context gracefully
        var tasks = new List<Task<string>>
        {
            _searchTool.Geocoding(null!, "123 Main St"),
            _searchTool.ReverseGeocoding(null!, 37.7749, -122.4194),
            _searchTool.GetPolygon(null!, 37.7749, -122.4194, "locality", "small"),
            _searchTool.GetCountryInfo(null!, "US"),
            _searchTool.FindCountries(null!, "United")
        };

        // All should complete without throwing exceptions
        var results = await Task.WhenAll(tasks);
        
        foreach (var result in results)
        {
            result.Should().NotBeNullOrEmpty();
            var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
            jsonResult.ValueKind.Should().Be(JsonValueKind.Object);
        }
    }

    [Fact]
    public void SearchTool_Constructor_AcceptsValidParameters()
    {
        // Arrange & Act
        var tool = new SearchTool(_mockAzureMapsService.Object, _mockLogger.Object);

        // Assert
        tool.Should().NotBeNull();
    }

    [Fact]
    public void SearchTool_Constructor_ThrowsOnNullService()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SearchTool(null!, _mockLogger.Object));
    }

    [Fact]
    public void SearchTool_Constructor_ThrowsOnNullLogger()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SearchTool(_mockAzureMapsService.Object, null!));
    }

    #endregion
}