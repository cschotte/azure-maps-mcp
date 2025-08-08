// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Moq;
using System.Net;
using FluentAssertions;

namespace Azure.Maps.Mcp.Tests.Helpers;

/// <summary>
/// Test helper class providing common test data and utilities for Azure Maps MCP tests
/// </summary>
public static class TestHelper
{
    /// <summary>
    /// Common test IP addresses for testing
    /// </summary>
    public static class TestIPs
    {
        public const string GoogleDNS_IPv4 = "8.8.8.8";
        public const string CloudflareDNS_IPv4 = "1.1.1.1";
        public const string OpenDNS_IPv4 = "208.67.222.222";
        public const string GoogleDNS_IPv6 = "2001:4860:4860::8888";
        public const string CloudflareDNS_IPv6 = "2606:4700:4700::1111";
        
        public const string Private_IPv4_Class_A = "10.0.0.1";
        public const string Private_IPv4_Class_B = "172.16.0.1";
        public const string Private_IPv4_Class_C = "192.168.1.1";
        public const string Loopback_IPv4 = "127.0.0.1";
        public const string Loopback_IPv6 = "::1";
        public const string LinkLocal_IPv6 = "fe80::1";
        
        public static readonly string[] ValidPublicIPs = new[]
        {
            GoogleDNS_IPv4,
            CloudflareDNS_IPv4,
            OpenDNS_IPv4,
            GoogleDNS_IPv6,
            CloudflareDNS_IPv6
        };
        
        public static readonly string[] ValidPrivateIPs = new[]
        {
            Private_IPv4_Class_A,
            Private_IPv4_Class_B,
            Private_IPv4_Class_C,
            Loopback_IPv4,
            Loopback_IPv6,
            LinkLocal_IPv6
        };
        
        public static readonly string[] InvalidIPs = new[]
        {
            "invalid-ip",
            "999.999.999.999",
            "not.an.ip.address",
            ":::",
            "256.256.256.256",
            "192.168.1.1.1",
            "2001:4860:4860::8888::1",
            "12345::",
            "::gggg"
        };
    }

    /// <summary>
    /// Common test country codes and data
    /// </summary>
    public static class TestCountries
    {
        public const string USA = "US";
        public const string Canada = "CA";
        public const string UnitedKingdom = "GB";
        public const string Germany = "DE";
        public const string Japan = "JP";
        public const string Australia = "AU";
        public const string Brazil = "BR";
        public const string Invalid = "XX";
        
        public static readonly string[] ValidCountryCodes = new[]
        {
            USA, Canada, UnitedKingdom, Germany, Japan, Australia, Brazil
        };
    }

    /// <summary>
    /// Creates a mock geolocation response for testing
    /// Note: This will be adjusted once we understand the actual Azure Maps API response type
    /// </summary>
    /// <param name="isoCode">The ISO country code to return</param>
    /// <param name="ipAddress">The IP address to associate with the response</param>
    /// <returns>A mocked Response containing the geolocation result</returns>
    public static Response<T> CreateMockResponse<T>(T value)
    {
        return Response.FromValue(value, Mock.Of<Response>());
    }

    /// <summary>
    /// Creates a mock failed response for testing error scenarios
    /// </summary>
    /// <returns>A mocked Response with null value</returns>
    public static Response<T> CreateMockFailedResponse<T>() where T : class
    {
        return Response.FromValue<T>(null!, Mock.Of<Response>());
    }

    /// <summary>
    /// Validates that a JSON response contains the expected success structure
    /// </summary>
    /// <param name="jsonResult">The JsonElement to validate</param>
    /// <param name="shouldBeSuccessful">Whether the response should indicate success</param>
    public static void ValidateJsonSuccessResponse(System.Text.Json.JsonElement jsonResult, bool shouldBeSuccessful = true)
    {
        if (shouldBeSuccessful)
        {
            jsonResult.TryGetProperty("success", out var successProp).Should().BeTrue();
            successProp.GetBoolean().Should().BeTrue();
        }
        else
        {
            jsonResult.TryGetProperty("success", out var successProp).Should().BeTrue();
            successProp.GetBoolean().Should().BeFalse();
        }
    }

    /// <summary>
    /// Validates that a JSON response contains an error message
    /// </summary>
    /// <param name="jsonResult">The JsonElement to validate</param>
    /// <param name="expectedErrorSubstring">Optional substring that should be contained in the error message</param>
    public static void ValidateJsonErrorResponse(System.Text.Json.JsonElement jsonResult, string expectedErrorSubstring = "")
    {
        jsonResult.TryGetProperty("error", out var errorProp).Should().BeTrue();
        var errorMessage = errorProp.GetString();
        errorMessage.Should().NotBeNullOrEmpty();
        
        if (!string.IsNullOrEmpty(expectedErrorSubstring))
        {
            errorMessage.Should().Contain(expectedErrorSubstring);
        }
    }

    /// <summary>
    /// Validates the structure of a batch operation result
    /// </summary>
    /// <param name="jsonResult">The JsonElement containing the batch result</param>
    /// <param name="expectedTotal">Expected total number of requests</param>
    /// <param name="expectedSuccessful">Expected number of successful requests</param>
    /// <param name="expectedFailed">Expected number of failed requests</param>
    public static void ValidateBatchResultStructure(
        System.Text.Json.JsonElement jsonResult, 
        int expectedTotal, 
        int expectedSuccessful, 
        int expectedFailed)
    {
        ValidateJsonSuccessResponse(jsonResult);
        
        var result = jsonResult.GetProperty("result");
        var summary = result.GetProperty("Summary");
        
        summary.GetProperty("TotalRequests").GetInt32().Should().Be(expectedTotal);
        summary.GetProperty("SuccessfulRequests").GetInt32().Should().Be(expectedSuccessful);
        summary.GetProperty("FailedRequests").GetInt32().Should().Be(expectedFailed);
        
        var results = result.GetProperty("Results");
        results.GetArrayLength().Should().Be(expectedTotal);
    }

    /// <summary>
    /// Validates the structure of an IP validation result
    /// </summary>
    /// <param name="jsonResult">The JsonElement containing the validation result</param>
    /// <param name="expectedIPAddress">The expected IP address in the result</param>
    /// <param name="expectedIsValid">Whether the IP should be marked as valid</param>
    public static void ValidateIPValidationResult(
        System.Text.Json.JsonElement jsonResult, 
        string expectedIPAddress, 
        bool expectedIsValid = true)
    {
        if (expectedIsValid)
        {
            ValidateJsonSuccessResponse(jsonResult);
            
            var result = jsonResult.GetProperty("result");
            var validation = result.GetProperty("ValidationResult");
            
            validation.GetProperty("IsValid").GetBoolean().Should().BeTrue();
            validation.GetProperty("IPAddress").GetString().Should().Be(expectedIPAddress);
            
            // Technical info should be present
            var techInfo = result.GetProperty("TechnicalInfo");
            techInfo.TryGetProperty("Bytes", out _).Should().BeTrue();
            techInfo.TryGetProperty("Timestamp", out _).Should().BeTrue();
        }
        else
        {
            jsonResult.TryGetProperty("isValid", out var isValidProp).Should().BeTrue();
            isValidProp.GetBoolean().Should().BeFalse();
        }
    }

    /// <summary>
    /// Creates test data for parameterized tests
    /// </summary>
    public static class TestData
    {
        public static IEnumerable<object[]> ValidIPAddressesWithExpectedProperties()
        {
            yield return new object[] { TestIPs.GoogleDNS_IPv4, true, false, false, false }; // Public IPv4
            yield return new object[] { TestIPs.Private_IPv4_Class_C, true, false, true, false }; // Private IPv4
            yield return new object[] { TestIPs.Loopback_IPv4, true, false, true, true }; // Loopback IPv4
            yield return new object[] { TestIPs.GoogleDNS_IPv6, false, true, false, false }; // Public IPv6
            yield return new object[] { TestIPs.Loopback_IPv6, false, true, true, true }; // Loopback IPv6
            yield return new object[] { TestIPs.LinkLocal_IPv6, false, true, true, false }; // Link-local IPv6
        }

        public static IEnumerable<object[]> InvalidIPAddresses()
        {
            foreach (var invalidIP in TestIPs.InvalidIPs)
            {
                yield return new object[] { invalidIP };
            }
        }

        public static IEnumerable<object[]> EmptyOrNullStrings()
        {
            yield return new object[] { null! };
            yield return new object[] { "" };
            yield return new object[] { " " };
            yield return new object[] { "\t" };
            yield return new object[] { "\n" };
        }

        public static IEnumerable<object[]> ValidPublicIPAddresses()
        {
            foreach (var publicIP in TestIPs.ValidPublicIPs)
            {
                yield return new object[] { publicIP };
            }
        }

        public static IEnumerable<object[]> ValidPrivateIPAddresses()
        {
            foreach (var privateIP in TestIPs.ValidPrivateIPs)
            {
                yield return new object[] { privateIP };
            }
        }
    }
}
