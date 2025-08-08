// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Maps.Mcp.Tools;
using Azure.Maps.Mcp.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Moq;
using System.Net;
using System.Text.Json;
using Xunit;
using FluentAssertions;
using Azure.Maps.Mcp.Tests.Helpers;

namespace Azure.Maps.Mcp.Tests.Tools;

/// <summary>
/// Tests for GeolocationTool focusing on business logic and validation
/// These tests focus on input validation, error handling, and output formatting
/// rather than mocking complex Azure SDK types
/// </summary>
public class GeolocationToolTests
{
    private readonly Mock<IAzureMapsService> _mockAzureMapsService;
    private readonly Mock<ILogger<GeolocationTool>> _mockLogger;
    private readonly GeolocationTool _geolocationTool;
    private readonly Mock<ToolInvocationContext> _mockContext;

    public GeolocationToolTests()
    {
        _mockAzureMapsService = new Mock<IAzureMapsService>();
        _mockLogger = new Mock<ILogger<GeolocationTool>>();
        _mockContext = new Mock<ToolInvocationContext>();

        // Mock the geolocation client - we'll focus on testing business logic
        var mockGeolocationClient = new Mock<Azure.Maps.Geolocation.MapsGeolocationClient>();
        _mockAzureMapsService.Setup(x => x.GeolocationClient).Returns(mockGeolocationClient.Object);
        
        _geolocationTool = new GeolocationTool(_mockAzureMapsService.Object, _mockLogger.Object);
    }

    #region Input Validation Tests

    [Theory]
    [MemberData(nameof(TestHelper.TestData.EmptyOrNullStrings), MemberType = typeof(TestHelper.TestData))]
    public async Task GetCountryCodeByIP_EmptyOrNullIPAddress_ReturnsError(string ipAddress)
    {
        // Act
        var result = await _geolocationTool.GetCountryCodeByIP(_mockContext.Object, ipAddress);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "IP address is required");
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidIPAddresses), MemberType = typeof(TestHelper.TestData))]
    public async Task GetCountryCodeByIP_InvalidIPAddress_ReturnsError(string ipAddress)
    {
        // Act
        var result = await _geolocationTool.GetCountryCodeByIP(_mockContext.Object, ipAddress);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "Invalid IP address format");
    }

    [Fact]
    public async Task GetCountryCodeBatch_EmptyArray_ReturnsError()
    {
        // Arrange
        var ipAddresses = Array.Empty<string>();

        // Act
        var result = await _geolocationTool.GetCountryCodeBatch(_mockContext.Object, ipAddresses);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "At least one IP address is required");
    }

    [Fact]
    public async Task GetCountryCodeBatch_NullArray_ReturnsError()
    {
        // Act
        var result = await _geolocationTool.GetCountryCodeBatch(_mockContext.Object, null!);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "At least one IP address is required");
    }

    [Fact]
    public async Task GetCountryCodeBatch_TooManyIPAddresses_ReturnsError()
    {
        // Arrange - Create 101 IP addresses to exceed the limit
        var ipAddresses = Enumerable.Range(1, 101)
            .Select(i => $"192.168.1.{i % 255 + 1}")
            .ToArray();

        // Act
        var result = await _geolocationTool.GetCountryCodeBatch(_mockContext.Object, ipAddresses);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "Maximum 100 IP addresses allowed per batch request");
    }

    #endregion

    #region ValidateIPAddress Tests

    [Theory]
    [MemberData(nameof(TestHelper.TestData.ValidIPAddressesWithExpectedProperties), MemberType = typeof(TestHelper.TestData))]
    public async Task ValidateIPAddress_ValidIPs_ReturnsCorrectValidation(
        string ipAddress, 
        bool expectedIsIPv4, 
        bool expectedIsIPv6, 
        bool expectedIsPrivate, 
        bool expectedIsLoopback)
    {
        // Act
        var result = await _geolocationTool.ValidateIPAddress(_mockContext.Object, ipAddress);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateIPValidationResult(jsonResult, ipAddress);
        
        var resultData = jsonResult.GetProperty("result");
        var validation = resultData.GetProperty("ValidationResult");
        
        validation.GetProperty("IsIPv4").GetBoolean().Should().Be(expectedIsIPv4);
        validation.GetProperty("IsIPv6").GetBoolean().Should().Be(expectedIsIPv6);
        validation.GetProperty("IsPrivate").GetBoolean().Should().Be(expectedIsPrivate);
        validation.GetProperty("IsLoopback").GetBoolean().Should().Be(expectedIsLoopback);
        
        // Technical info should be present
        var techInfo = resultData.GetProperty("TechnicalInfo");
        techInfo.GetProperty("Bytes").Should().NotBeNull();
        techInfo.GetProperty("Timestamp").Should().NotBeNull();
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.EmptyOrNullStrings), MemberType = typeof(TestHelper.TestData))]
    public async Task ValidateIPAddress_EmptyOrNullIP_ReturnsError(string ipAddress)
    {
        // Act
        var result = await _geolocationTool.ValidateIPAddress(_mockContext.Object, ipAddress);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "IP address is required");
    }

    [Theory]
    [MemberData(nameof(TestHelper.TestData.InvalidIPAddresses), MemberType = typeof(TestHelper.TestData))]
    public async Task ValidateIPAddress_InvalidIP_ReturnsValidationError(string ipAddress)
    {
        // Act
        var result = await _geolocationTool.ValidateIPAddress(_mockContext.Object, ipAddress);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateJsonErrorResponse(jsonResult, "Invalid IP address format");
        TestHelper.ValidateIPValidationResult(jsonResult, ipAddress, expectedIsValid: false);
    }

    #endregion

    #region Private Method Tests (IsPrivateIP)

    [Theory]
    [InlineData("10.0.0.1", true)]        // 10.0.0.0/8
    [InlineData("10.255.255.255", true)]  // 10.0.0.0/8 boundary
    [InlineData("11.0.0.1", false)]       // Just outside 10.0.0.0/8
    [InlineData("172.16.0.1", true)]      // 172.16.0.0/12
    [InlineData("172.31.255.255", true)]  // 172.16.0.0/12 boundary
    [InlineData("172.15.255.255", false)] // Just outside 172.16.0.0/12
    [InlineData("172.32.0.1", false)]     // Just outside 172.16.0.0/12
    [InlineData("192.168.1.1", true)]     // 192.168.0.0/16
    [InlineData("192.168.255.255", true)] // 192.168.0.0/16 boundary
    [InlineData("192.167.255.255", false)] // Just outside 192.168.0.0/16
    [InlineData("127.0.0.1", true)]       // Loopback
    [InlineData("127.255.255.255", true)] // Loopback boundary
    [InlineData("8.8.8.8", false)]       // Public IP
    [InlineData("1.1.1.1", false)]       // Public IP
    public void IsPrivateIP_IPv4Addresses_ReturnsCorrectResult(string ipAddress, bool expectedIsPrivate)
    {
        // Arrange
        var parsedIP = IPAddress.Parse(ipAddress);
        var method = typeof(GeolocationTool).GetMethod("IsPrivateIP", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = (bool)method!.Invoke(null, new object[] { parsedIP })!;

        // Assert
        result.Should().Be(expectedIsPrivate);
    }

    [Theory]
    [InlineData("::1", true)]                    // IPv6 Loopback
    [InlineData("fe80::1", true)]               // IPv6 Link-local
    [InlineData("fec0::1", true)]               // IPv6 Site-local (deprecated but still private)
    [InlineData("2001:4860:4860::8888", false)] // Public IPv6 (Google DNS)
    [InlineData("2606:4700:4700::1111", false)] // Public IPv6 (Cloudflare DNS)
    public void IsPrivateIP_IPv6Addresses_ReturnsCorrectResult(string ipAddress, bool expectedIsPrivate)
    {
        // Arrange
        var parsedIP = IPAddress.Parse(ipAddress);
        var method = typeof(GeolocationTool).GetMethod("IsPrivateIP", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = (bool)method!.Invoke(null, new object[] { parsedIP })!;

        // Assert
        result.Should().Be(expectedIsPrivate);
    }

    #endregion

    #region Constructor and Dependency Tests

    [Fact]
    public void GeolocationTool_ConstructorWithValidDependencies_ShouldNotThrow()
    {
        // Arrange & Act
        var action = () => new GeolocationTool(_mockAzureMapsService.Object, _mockLogger.Object);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void GeolocationTool_Constructor_ShouldSetupGeolocationClientCorrectly()
    {
        // Arrange & Act
        var mockService = new Mock<IAzureMapsService>();
        var mockGeolocationClient = new Mock<Azure.Maps.Geolocation.MapsGeolocationClient>();
        mockService.Setup(x => x.GeolocationClient).Returns(mockGeolocationClient.Object);
        
        var tool = new GeolocationTool(mockService.Object, _mockLogger.Object);

        // Assert
        mockService.Verify(x => x.GeolocationClient, Times.Once);
    }

    #endregion

    #region Response Format Tests

    [Fact]
    public async Task ValidateIPAddress_IPv4_ReturnsCorrectJSONStructure()
    {
        // Arrange
        var ipAddress = TestHelper.TestIPs.GoogleDNS_IPv4;

        // Act
        var result = await _geolocationTool.ValidateIPAddress(_mockContext.Object, ipAddress);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        
        // Verify top-level structure
        jsonResult.TryGetProperty("success", out var successProp).Should().BeTrue();
        successProp.GetBoolean().Should().BeTrue();
        
        jsonResult.TryGetProperty("result", out var resultProp).Should().BeTrue();
        
        // Verify validation result structure
        var validation = resultProp.GetProperty("ValidationResult");
        validation.TryGetProperty("IsValid", out _).Should().BeTrue();
        validation.TryGetProperty("IPAddress", out _).Should().BeTrue();
        validation.TryGetProperty("AddressFamily", out _).Should().BeTrue();
        validation.TryGetProperty("IsIPv4", out _).Should().BeTrue();
        validation.TryGetProperty("IsIPv6", out _).Should().BeTrue();
        validation.TryGetProperty("IsLoopback", out _).Should().BeTrue();
        validation.TryGetProperty("IsPrivate", out _).Should().BeTrue();
        validation.TryGetProperty("Scope", out _).Should().BeTrue();
        
        // Verify technical info structure
        var techInfo = resultProp.GetProperty("TechnicalInfo");
        techInfo.TryGetProperty("Bytes", out _).Should().BeTrue();
        techInfo.TryGetProperty("ScopeId", out _).Should().BeTrue();
        techInfo.TryGetProperty("Timestamp", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ValidateIPAddress_IPv6_ReturnsCorrectJSONStructure()
    {
        // Arrange
        var ipAddress = TestHelper.TestIPs.GoogleDNS_IPv6;

        // Act
        var result = await _geolocationTool.ValidateIPAddress(_mockContext.Object, ipAddress);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
        TestHelper.ValidateIPValidationResult(jsonResult, ipAddress);
        
        var resultData = jsonResult.GetProperty("result");
        var validation = resultData.GetProperty("ValidationResult");
        
        // IPv6-specific validations
        validation.GetProperty("IsIPv6").GetBoolean().Should().BeTrue();
        validation.GetProperty("IsIPv4").GetBoolean().Should().BeFalse();
        validation.GetProperty("AddressFamily").GetString().Should().Be("InterNetworkV6");
        
        var techInfo = resultData.GetProperty("TechnicalInfo");
        techInfo.GetProperty("ScopeId").Should().NotBeNull();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ValidateIPAddress_IPv6WithSpecialScopes_ReturnsCorrectScope()
    {
        // Test various IPv6 scope types
        var testCases = new[]
        {
            (IP: "fe80::1", ExpectedScope: "Link-Local"),
            (IP: "fec0::1", ExpectedScope: "Site-Local"),
            (IP: "ff02::1", ExpectedScope: "Multicast"),
            (IP: "2001:db8::1", ExpectedScope: "Global")
        };

        foreach (var testCase in testCases)
        {
            // Act
            var result = await _geolocationTool.ValidateIPAddress(_mockContext.Object, testCase.IP);

            // Assert
            result.Should().NotBeNullOrEmpty();
            
            var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
            TestHelper.ValidateIPValidationResult(jsonResult, testCase.IP);
            
            var resultData = jsonResult.GetProperty("result");
            var validation = resultData.GetProperty("ValidationResult");
            validation.GetProperty("Scope").GetString().Should().Be(testCase.ExpectedScope);
        }
    }

    [Fact]
    public async Task ValidateIPAddress_ConsistentResultsOnMultipleCalls()
    {
        // Arrange
        var ipAddress = TestHelper.TestIPs.GoogleDNS_IPv4;

        // Act - Call multiple times
        var results = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var result = await _geolocationTool.ValidateIPAddress(_mockContext.Object, ipAddress);
            results.Add(result);
        }

        // Assert - All results should be structurally consistent
        results.Should().AllSatisfy(result =>
        {
            var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
            TestHelper.ValidateIPValidationResult(jsonResult, ipAddress);
        });

        // Check that the core validation properties are consistent across calls
        var firstResult = JsonSerializer.Deserialize<JsonElement>(results[0]);
        var lastResult = JsonSerializer.Deserialize<JsonElement>(results[4]);
        
        var firstValidation = firstResult.GetProperty("result").GetProperty("ValidationResult");
        var lastValidation = lastResult.GetProperty("result").GetProperty("ValidationResult");
        
        firstValidation.GetProperty("IsValid").GetBoolean().Should().Be(lastValidation.GetProperty("IsValid").GetBoolean());
        firstValidation.GetProperty("IsIPv4").GetBoolean().Should().Be(lastValidation.GetProperty("IsIPv4").GetBoolean());
        firstValidation.GetProperty("IsPrivate").GetBoolean().Should().Be(lastValidation.GetProperty("IsPrivate").GetBoolean());
        firstValidation.GetProperty("AddressFamily").GetString().Should().Be(lastValidation.GetProperty("AddressFamily").GetString());
    }

    #endregion
}
