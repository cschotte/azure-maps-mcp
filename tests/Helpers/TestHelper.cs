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
    /// Common test addresses and locations for search testing
    /// </summary>
    public static class TestAddresses
    {
        public const string WhiteHouse = "1600 Pennsylvania Avenue, Washington, DC";
        public const string EiffelTower = "Eiffel Tower, Paris, France";
        public const string TimesSquare = "Times Square, New York, NY";
        public const string SeattleAddress = "123 Main Street, Seattle, WA";
        public const string PartialAddress = "Main Street";
        public const string Landmark = "Space Needle";
        
        public static readonly string[] ValidAddresses = new[]
        {
            WhiteHouse, EiffelTower, TimesSquare, SeattleAddress, Landmark
        };
        
        public static readonly string[] InvalidAddresses = new[]
        {
            "xyzxyzxyz123nonexistentaddress",
            "!@#$%^&*()",
            "123456789012345678901234567890"
        };
    }

    /// <summary>
    /// Common test coordinates for reverse geocoding and polygon testing
    /// </summary>
    public static class TestCoordinates
    {
        // Seattle coordinates
        public const double SeattleLat = 47.6062;
        public const double SeattleLon = -122.3321;
        
        // New York coordinates  
        public const double NewYorkLat = 40.7128;
        public const double NewYorkLon = -74.0060;
        
        // London coordinates
        public const double LondonLat = 51.5074;
        public const double LondonLon = -0.1278;
        
        // Tokyo coordinates
        public const double TokyoLat = 35.6762;
        public const double TokyoLon = 139.6503;
        
        // Invalid coordinates
        public const double InvalidLatHigh = 91.0;
        public const double InvalidLatLow = -91.0;
        public const double InvalidLonHigh = 181.0;
        public const double InvalidLonLow = -181.0;
        
        public static readonly (double lat, double lon)[] ValidCoordinates = new[]
        {
            (SeattleLat, SeattleLon),
            (NewYorkLat, NewYorkLon),
            (LondonLat, LondonLon),
            (TokyoLat, TokyoLon)
        };
        
        public static readonly (double lat, double lon)[] InvalidCoordinates = new[]
        {
            (InvalidLatHigh, SeattleLon),
            (InvalidLatLow, SeattleLon),
            (SeattleLat, InvalidLonHigh),
            (SeattleLat, InvalidLonLow)
        };
    }

    /// <summary>
    /// Test data for polygon boundary types and resolutions
    /// </summary>
    public static class TestPolygonOptions
    {
        public static readonly string[] ValidResultTypes = new[]
        {
            "locality", "postalCode", "adminDistrict", "countryRegion"
        };
        
        public static readonly string[] ValidResolutions = new[]
        {
            "small", "medium", "large"
        };
        
        public static readonly string[] InvalidResultTypes = new[]
        {
            "invalid", "wrongtype", "municipality", "district"
        };
        
        public static readonly string[] InvalidResolutions = new[]
        {
            "tiny", "huge", "extra", "minimal"
        };
    }

    /// <summary>
    /// Test data for country search terms
    /// </summary>
    public static class TestSearchTerms
    {
        public static readonly string[] ValidSearchTerms = new[]
        {
            "United", "Europe", "America", "Island", "Kingdom", "Republic"
        };
        
        public static readonly string[] ValidContinents = new[]
        {
            "Europe", "Asia", "Africa", "North America", "South America", "Oceania", "Antarctica"
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
    /// Validates geocoding response structure and content
    /// </summary>
    /// <param name="jsonResult">The JSON response from geocoding</param>
    /// <param name="expectedResultCount">Expected number of results (or null for any)</param>
    public static void ValidateGeocodingResponse(System.Text.Json.JsonElement jsonResult, int? expectedResultCount = null)
    {
        ValidateJsonSuccessResponse(jsonResult);
        
        jsonResult.TryGetProperty("results", out var resultsArray).Should().BeTrue();
        resultsArray.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        
        if (expectedResultCount.HasValue)
        {
            resultsArray.GetArrayLength().Should().Be(expectedResultCount.Value);
        }
        else
        {
            resultsArray.GetArrayLength().Should().BeGreaterThan(0);
        }
        
        // Validate first result structure
        var firstResult = resultsArray[0];
        firstResult.TryGetProperty("Coordinates", out var coords).Should().BeTrue();
        coords.TryGetProperty("Latitude", out _).Should().BeTrue();
        coords.TryGetProperty("Longitude", out _).Should().BeTrue();
        firstResult.TryGetProperty("AddressDetails", out _).Should().BeTrue();
    }

    /// <summary>
    /// Validates reverse geocoding response structure
    /// </summary>
    /// <param name="jsonResult">The JSON response from reverse geocoding</param>
    public static void ValidateReverseGeocodingResponse(System.Text.Json.JsonElement jsonResult)
    {
        ValidateJsonSuccessResponse(jsonResult);
        
        var result = jsonResult.GetProperty("result");
        result.TryGetProperty("Coordinates", out var coords).Should().BeTrue();
        coords.TryGetProperty("Latitude", out _).Should().BeTrue();
        coords.TryGetProperty("Longitude", out _).Should().BeTrue();
        result.TryGetProperty("AddressDetails", out _).Should().BeTrue();
    }

    /// <summary>
    /// Validates polygon response structure
    /// </summary>
    /// <param name="jsonResult">The JSON response from polygon query</param>
    /// <param name="expectedPolygonCount">Expected number of polygons (or null for any)</param>
    public static void ValidatePolygonResponse(System.Text.Json.JsonElement jsonResult, int? expectedPolygonCount = null)
    {
        ValidateJsonSuccessResponse(jsonResult);
        
        var result = jsonResult.GetProperty("result");
        result.TryGetProperty("BoundaryInfo", out var boundaryInfo).Should().BeTrue();
        result.TryGetProperty("PolygonCount", out var polygonCount).Should().BeTrue();
        result.TryGetProperty("Polygons", out var polygons).Should().BeTrue();
        
        polygons.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        
        if (expectedPolygonCount.HasValue)
        {
            polygons.GetArrayLength().Should().Be(expectedPolygonCount.Value);
            polygonCount.GetInt32().Should().Be(expectedPolygonCount.Value);
        }
        else
        {
            polygons.GetArrayLength().Should().BeGreaterThan(0);
            polygonCount.GetInt32().Should().BeGreaterThan(0);
        }
        
        // Validate boundary info structure
        boundaryInfo.TryGetProperty("ResultType", out _).Should().BeTrue();
        boundaryInfo.TryGetProperty("Resolution", out _).Should().BeTrue();
        boundaryInfo.TryGetProperty("QueryCoordinates", out _).Should().BeTrue();
    }

    /// <summary>
    /// Validates country info response structure
    /// </summary>
    /// <param name="jsonResult">The JSON response from country info query</param>
    public static void ValidateCountryInfoResponse(System.Text.Json.JsonElement jsonResult)
    {
        ValidateJsonSuccessResponse(jsonResult);
        
        var country = jsonResult.GetProperty("country");
        country.TryGetProperty("CountryName", out _).Should().BeTrue();
        country.TryGetProperty("CountryShortCode", out _).Should().BeTrue();
    }

    /// <summary>
    /// Validates country search response structure
    /// </summary>
    /// <param name="jsonResult">The JSON response from country search</param>
    /// <param name="expectedMinResults">Minimum expected number of results</param>
    public static void ValidateCountrySearchResponse(System.Text.Json.JsonElement jsonResult, int expectedMinResults = 1)
    {
        ValidateJsonSuccessResponse(jsonResult);
        
        var result = jsonResult.GetProperty("result");
        result.TryGetProperty("SearchTerm", out _).Should().BeTrue();
        result.TryGetProperty("TotalMatches", out var totalMatches).Should().BeTrue();
        result.TryGetProperty("Countries", out var countries).Should().BeTrue();
        
        countries.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        countries.GetArrayLength().Should().BeGreaterThanOrEqualTo(expectedMinResults);
        totalMatches.GetInt32().Should().BeGreaterThanOrEqualTo(expectedMinResults);
    }

    /// <summary>
    /// Test data for routing operations including travel modes, route types, and routing scenarios
    /// </summary>
    public static class TestRouting
    {
        // Travel modes
        public static readonly string[] ValidTravelModes = new[]
        {
            "car", "truck", "taxi", "bus", "van", "motorcycle", "bicycle", "pedestrian"
        };
        
        public static readonly string[] InvalidTravelModes = new[]
        {
            "airplane", "boat", "train", "invalid", "", "walking"
        };
        
        // Route types
        public static readonly string[] ValidRouteTypes = new[]
        {
            "fastest", "shortest"
        };
        
        public static readonly string[] InvalidRouteTypes = new[]
        {
            "cheapest", "scenic", "invalid", "", "optimal"
        };
        
        // Avoid options
        public static readonly string[] ValidAvoidOptions = new[]
        {
            "true", "false"
        };
        
        public static readonly string[] InvalidAvoidOptions = new[]
        {
            "yes", "no", "1", "0", "maybe", ""
        };
        
        // Time budgets for route range (in seconds)
        public static readonly int[] ValidTimeBudgets = new[]
        {
            300,   // 5 minutes
            900,   // 15 minutes  
            1800,  // 30 minutes
            3600,  // 1 hour
            7200   // 2 hours
        };
        
        public static readonly int[] InvalidTimeBudgets = new[]
        {
            -1, 0, -3600, int.MaxValue
        };
        
        // Distance budgets for route range (in meters)
        public static readonly int[] ValidDistanceBudgets = new[]
        {
            1000,   // 1 km
            5000,   // 5 km
            10000,  // 10 km
            25000,  // 25 km
            50000   // 50 km
        };
        
        public static readonly int[] InvalidDistanceBudgets = new[]
        {
            -1, 0, -5000, int.MaxValue
        };

        // Common routing coordinate pairs (origin, destination)
        public static readonly ((double lat, double lon) origin, (double lat, double lon) destination)[] ValidRoutePairs = new[]
        {
            // Seattle to New York (cross-country)
            ((TestCoordinates.SeattleLat, TestCoordinates.SeattleLon), (TestCoordinates.NewYorkLat, TestCoordinates.NewYorkLon)),
            // London to nearby coordinate (short distance)
            ((TestCoordinates.LondonLat, TestCoordinates.LondonLon), (51.5085, -0.1257)),
            // Tokyo local route
            ((TestCoordinates.TokyoLat, TestCoordinates.TokyoLon), (35.6895, 139.6917))
        };

        // Multi-waypoint routes for testing
        public static readonly (double lat, double lon)[][] ValidMultiWaypointRoutes = new[]
        {
            // Seattle -> Portland -> San Francisco
            new[] 
            { 
                (TestCoordinates.SeattleLat, TestCoordinates.SeattleLon),
                (45.5152, -122.6784), // Portland
                (37.7749, -122.4194)  // San Francisco
            },
            // European cities tour
            new[]
            {
                (TestCoordinates.LondonLat, TestCoordinates.LondonLon),
                (48.8566, 2.3522),   // Paris
                (52.5200, 13.4050),  // Berlin
                (41.9028, 12.4964)   // Rome
            }
        };

        // Cross-border routes for country analysis
        public static readonly (double lat, double lon)[][] ValidCrossBorderRoutes = new[]
        {
            // USA to Canada (Vancouver to Seattle)
            new[]
            {
                (49.2827, -123.1207), // Vancouver, Canada
                (TestCoordinates.SeattleLat, TestCoordinates.SeattleLon) // Seattle, USA
            },
            // Germany to France
            new[]
            {
                (48.1351, 11.5820),  // Munich, Germany
                (48.8566, 2.3522)    // Paris, France
            }
        };

        // Matrix test data - multiple origins and destinations
        public static readonly (double lat, double lon)[] MatrixOrigins = new[]
        {
            (TestCoordinates.SeattleLat, TestCoordinates.SeattleLon),
            (TestCoordinates.NewYorkLat, TestCoordinates.NewYorkLon)
        };

        public static readonly (double lat, double lon)[] MatrixDestinations = new[]
        {
            (TestCoordinates.LondonLat, TestCoordinates.LondonLon),
            (TestCoordinates.TokyoLat, TestCoordinates.TokyoLon)
        };
    }

    /// <summary>
    /// Test data for render operations including map styles, dimensions, and rendering scenarios
    /// </summary>
    public static class TestRender
    {
        // Map styles
        public static readonly string[] ValidMapStyles = new[]
        {
            "road", "satellite", "hybrid"
        };
        
        public static readonly string[] InvalidMapStyles = new[]
        {
            "terrain", "dark", "light", "invalid", "", "topographic"
        };
        
        // Zoom levels
        public static readonly int[] ValidZoomLevels = new[]
        {
            1, 5, 10, 15, 20
        };
        
        public static readonly int[] InvalidZoomLevels = new[]
        {
            0, -1, 21, 25, 100
        };
        
        // Image dimensions
        public static readonly int[] ValidDimensions = new[]
        {
            1, 256, 512, 1024, 2048, 4096, 8192
        };
        
        public static readonly int[] InvalidDimensions = new[]
        {
            0, -1, 8193, 10000, -512
        };

        // Valid bounding boxes
        public static readonly string[] ValidBoundingBoxes = new[]
        {
            // Seattle area
            "{\"west\": -122.4, \"south\": 47.5, \"east\": -122.2, \"north\": 47.7}",
            // New York area
            "{\"west\": -74.1, \"south\": 40.6, \"east\": -73.9, \"north\": 40.8}",
            // London area
            "{\"west\": -0.2, \"south\": 51.4, \"east\": 0.0, \"north\": 51.6}",
            // Small area (valid but minimal)
            "{\"west\": -1.0, \"south\": 1.0, \"east\": 1.0, \"north\": 2.0}"
        };

        public static readonly string[] InvalidBoundingBoxes = new[]
        {
            // Invalid JSON
            "{\"west\": -122.4, \"south\": 47.5, \"east\": -122.2, \"north\": 47.7",
            "invalid json",
            "",
            "null",
            // Missing required properties
            "{\"west\": -122.4, \"south\": 47.5, \"east\": -122.2}",
            "{\"south\": 47.5, \"east\": -122.2, \"north\": 47.7}",
            "{\"west\": -122.4, \"east\": -122.2, \"north\": 47.7}",
            "{\"west\": -122.4, \"south\": 47.5, \"north\": 47.7}",
            // Invalid coordinate values
            "{\"west\": 181, \"south\": 47.5, \"east\": -122.2, \"north\": 47.7}",
            "{\"west\": -122.4, \"south\": -91, \"east\": -122.2, \"north\": 47.7}",
            "{\"west\": -122.4, \"south\": 47.5, \"east\": -181, \"north\": 47.7}",
            "{\"west\": -122.4, \"south\": 47.5, \"east\": -122.2, \"north\": 91}",
            // Inverted bounds (west > east, south > north)
            "{\"west\": -122.2, \"south\": 47.7, \"east\": -122.4, \"north\": 47.5}"
        };

        // Test markers
        public static readonly object[][] ValidMarkers = new object[][]
        {
            // Single marker
            new object[] { new[] { 
                new { latitude = TestCoordinates.SeattleLat, longitude = TestCoordinates.SeattleLon, label = "Seattle", color = "red" } 
            }},
            // Multiple markers
            new object[] { new[] { 
                new { latitude = TestCoordinates.SeattleLat, longitude = TestCoordinates.SeattleLon, label = "Seattle", color = "red" },
                new { latitude = TestCoordinates.NewYorkLat, longitude = TestCoordinates.NewYorkLon, label = "NYC", color = "blue" }
            }},
            // Marker without optional properties
            new object[] { new[] { 
                new { latitude = TestCoordinates.LondonLat, longitude = TestCoordinates.LondonLon }
            }}
        };

        public static readonly object[][] InvalidMarkers = new object[][]
        {
            // Invalid coordinates
            new object[] { new[] { 
                new { latitude = 91.0, longitude = TestCoordinates.SeattleLon, label = "Invalid", color = "red" } 
            }},
            new object[] { new[] { 
                new { latitude = TestCoordinates.SeattleLat, longitude = 181.0, label = "Invalid", color = "red" } 
            }}
        };

        // Test paths
        public static readonly object[][] ValidPaths = new object[][]
        {
            // Simple path
            new object[] { new[] { 
                new { 
                    coordinates = new[] {
                        new { latitude = TestCoordinates.SeattleLat, longitude = TestCoordinates.SeattleLon },
                        new { latitude = TestCoordinates.NewYorkLat, longitude = TestCoordinates.NewYorkLon }
                    },
                    color = "blue",
                    width = 3
                }
            }},
            // Multiple paths
            new object[] { new[] { 
                new { 
                    coordinates = new[] {
                        new { latitude = TestCoordinates.SeattleLat, longitude = TestCoordinates.SeattleLon },
                        new { latitude = 45.5152, longitude = -122.6784 } // Portland
                    },
                    color = "red",
                    width = 2
                },
                new { 
                    coordinates = new[] {
                        new { latitude = TestCoordinates.LondonLat, longitude = TestCoordinates.LondonLon },
                        new { latitude = 48.8566, longitude = 2.3522 } // Paris
                    },
                    color = "green",
                    width = 4
                }
            }}
        };

        public static readonly object[][] InvalidPaths = new object[][]
        {
            // Single coordinate (need at least 2)
            new object[] { new[] { 
                new { 
                    coordinates = new[] {
                        new { latitude = TestCoordinates.SeattleLat, longitude = TestCoordinates.SeattleLon }
                    },
                    color = "blue",
                    width = 3
                }
            }},
            // Invalid coordinates in path
            new object[] { new[] { 
                new { 
                    coordinates = new[] {
                        new { latitude = 91.0, longitude = TestCoordinates.SeattleLon },
                        new { latitude = TestCoordinates.NewYorkLat, longitude = TestCoordinates.NewYorkLon }
                    },
                    color = "blue",
                    width = 3
                }
            }}
        };

        // Common bounding box tests
        public static readonly (string name, string bbox)[] BoundingBoxTestCases = new[]
        {
            ("Seattle", "{\"west\": -122.4, \"south\": 47.5, \"east\": -122.2, \"north\": 47.7}"),
            ("New York", "{\"west\": -74.1, \"south\": 40.6, \"east\": -73.9, \"north\": 40.8}"),
            ("London", "{\"west\": -0.2, \"south\": 51.4, \"east\": 0.0, \"north\": 51.6}"),
            ("World", "{\"west\": -180, \"south\": -85, \"east\": 180, \"north\": 85}")
        };
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

        public static IEnumerable<object[]> ValidAddresses()
        {
            foreach (var address in TestAddresses.ValidAddresses)
            {
                yield return new object[] { address };
            }
        }

        public static IEnumerable<object[]> InvalidAddresses()
        {
            foreach (var address in TestAddresses.InvalidAddresses)
            {
                yield return new object[] { address };
            }
        }

        public static IEnumerable<object[]> ValidCoordinates()
        {
            foreach (var (lat, lon) in TestCoordinates.ValidCoordinates)
            {
                yield return new object[] { lat, lon };
            }
        }

        public static IEnumerable<object[]> InvalidCoordinates()
        {
            foreach (var (lat, lon) in TestCoordinates.InvalidCoordinates)
            {
                yield return new object[] { lat, lon };
            }
        }

        public static IEnumerable<object[]> ValidPolygonResultTypes()
        {
            foreach (var resultType in TestPolygonOptions.ValidResultTypes)
            {
                yield return new object[] { resultType };
            }
        }

        public static IEnumerable<object[]> InvalidPolygonResultTypes()
        {
            foreach (var resultType in TestPolygonOptions.InvalidResultTypes)
            {
                yield return new object[] { resultType };
            }
        }

        public static IEnumerable<object[]> ValidPolygonResolutions()
        {
            foreach (var resolution in TestPolygonOptions.ValidResolutions)
            {
                yield return new object[] { resolution };
            }
        }

        public static IEnumerable<object[]> InvalidPolygonResolutions()
        {
            foreach (var resolution in TestPolygonOptions.InvalidResolutions)
            {
                yield return new object[] { resolution };
            }
        }

        public static IEnumerable<object[]> ValidCountryCodes()
        {
            foreach (var code in TestCountries.ValidCountryCodes)
            {
                yield return new object[] { code };
            }
        }

        public static IEnumerable<object[]> ValidSearchTerms()
        {
            foreach (var term in TestSearchTerms.ValidSearchTerms)
            {
                yield return new object[] { term };
            }
        }

        public static IEnumerable<object[]> ValidContinents()
        {
            foreach (var continent in TestSearchTerms.ValidContinents)
            {
                yield return new object[] { continent };
            }
        }

        // Routing test data methods
        public static IEnumerable<object[]> ValidTravelModes()
        {
            foreach (var mode in TestRouting.ValidTravelModes)
            {
                yield return new object[] { mode };
            }
        }

        public static IEnumerable<object[]> InvalidTravelModes()
        {
            foreach (var mode in TestRouting.InvalidTravelModes)
            {
                yield return new object[] { mode };
            }
        }

        public static IEnumerable<object[]> ValidRouteTypes()
        {
            foreach (var type in TestRouting.ValidRouteTypes)
            {
                yield return new object[] { type };
            }
        }

        public static IEnumerable<object[]> InvalidRouteTypes()
        {
            foreach (var type in TestRouting.InvalidRouteTypes)
            {
                yield return new object[] { type };
            }
        }

        public static IEnumerable<object[]> ValidAvoidOptions()
        {
            foreach (var option in TestRouting.ValidAvoidOptions)
            {
                yield return new object[] { option };
            }
        }

        public static IEnumerable<object[]> InvalidAvoidOptions()
        {
            foreach (var option in TestRouting.InvalidAvoidOptions)
            {
                yield return new object[] { option };
            }
        }

        public static IEnumerable<object[]> ValidTimeBudgets()
        {
            foreach (var budget in TestRouting.ValidTimeBudgets)
            {
                yield return new object[] { budget };
            }
        }

        public static IEnumerable<object[]> InvalidTimeBudgets()
        {
            foreach (var budget in TestRouting.InvalidTimeBudgets)
            {
                yield return new object[] { budget };
            }
        }

        public static IEnumerable<object[]> ValidDistanceBudgets()
        {
            foreach (var budget in TestRouting.ValidDistanceBudgets)
            {
                yield return new object[] { budget };
            }
        }

        public static IEnumerable<object[]> InvalidDistanceBudgets()
        {
            foreach (var budget in TestRouting.InvalidDistanceBudgets)
            {
                yield return new object[] { budget };
            }
        }

        public static IEnumerable<object[]> ValidRoutePairs()
        {
            foreach (var pair in TestRouting.ValidRoutePairs)
            {
                yield return new object[] { pair.origin.lat, pair.origin.lon, pair.destination.lat, pair.destination.lon };
            }
        }

        public static IEnumerable<object[]> ValidMultiWaypointRoutes()
        {
            foreach (var route in TestRouting.ValidMultiWaypointRoutes)
            {
                yield return new object[] { route };
            }
        }

        public static IEnumerable<object[]> ValidCrossBorderRoutes()
        {
            foreach (var route in TestRouting.ValidCrossBorderRoutes)
            {
                yield return new object[] { route };
            }
        }

        public static IEnumerable<object[]> MatrixOriginsAndDestinations()
        {
            yield return new object[] { TestRouting.MatrixOrigins, TestRouting.MatrixDestinations };
        }

        // Render test data methods
        public static IEnumerable<object[]> ValidMapStyles()
        {
            foreach (var style in TestRender.ValidMapStyles)
            {
                yield return new object[] { style };
            }
        }

        public static IEnumerable<object[]> InvalidMapStyles()
        {
            foreach (var style in TestRender.InvalidMapStyles)
            {
                yield return new object[] { style };
            }
        }

        public static IEnumerable<object[]> ValidZoomLevels()
        {
            foreach (var zoom in TestRender.ValidZoomLevels)
            {
                yield return new object[] { zoom };
            }
        }

        public static IEnumerable<object[]> InvalidZoomLevels()
        {
            foreach (var zoom in TestRender.InvalidZoomLevels)
            {
                yield return new object[] { zoom };
            }
        }

        public static IEnumerable<object[]> ValidDimensions()
        {
            foreach (var dimension in TestRender.ValidDimensions)
            {
                yield return new object[] { dimension };
            }
        }

        public static IEnumerable<object[]> InvalidDimensions()
        {
            foreach (var dimension in TestRender.InvalidDimensions)
            {
                yield return new object[] { dimension };
            }
        }

        public static IEnumerable<object[]> ValidBoundingBoxes()
        {
            foreach (var bbox in TestRender.ValidBoundingBoxes)
            {
                yield return new object[] { bbox };
            }
        }

        public static IEnumerable<object[]> InvalidBoundingBoxes()
        {
            foreach (var bbox in TestRender.InvalidBoundingBoxes)
            {
                yield return new object[] { bbox };
            }
        }

        public static IEnumerable<object[]> ValidMarkers()
        {
            return TestRender.ValidMarkers;
        }

        public static IEnumerable<object[]> InvalidMarkers()
        {
            return TestRender.InvalidMarkers;
        }

        public static IEnumerable<object[]> ValidPaths()
        {
            return TestRender.ValidPaths;
        }

        public static IEnumerable<object[]> InvalidPaths()
        {
            return TestRender.InvalidPaths;
        }

        public static IEnumerable<object[]> BoundingBoxTestCases()
        {
            foreach (var testCase in TestRender.BoundingBoxTestCases)
            {
                yield return new object[] { testCase.name, testCase.bbox };
            }
        }
    }

    /// <summary>
    /// Validation methods for routing responses
    /// </summary>
    public static class RoutingValidation
    {
        /// <summary>
        /// Validates a successful route directions response
        /// </summary>
        public static void ValidateRouteDirectionsResponse(string jsonResult)
        {
            jsonResult.Should().NotBeNullOrEmpty();
            
            var result = JsonDocument.Parse(jsonResult).RootElement;
            result.TryGetProperty("formatVersion", out _).Should().BeTrue();
            result.TryGetProperty("routes", out var routes).Should().BeTrue();
            
            routes.ValueKind.Should().Be(JsonValueKind.Array);
            routes.GetArrayLength().Should().BeGreaterThan(0);
            
            var firstRoute = routes[0];
            firstRoute.TryGetProperty("summary", out var summary).Should().BeTrue();
            firstRoute.TryGetProperty("legs", out var legs).Should().BeTrue();
            
            // Validate summary contains expected fields
            summary.TryGetProperty("lengthInMeters", out _).Should().BeTrue();
            summary.TryGetProperty("travelTimeInSeconds", out _).Should().BeTrue();
            
            // Validate legs array
            legs.ValueKind.Should().Be(JsonValueKind.Array);
            legs.GetArrayLength().Should().BeGreaterThan(0);
        }

        /// <summary>
        /// Validates a successful route matrix response
        /// </summary>
        public static void ValidateRouteMatrixResponse(string jsonResult, int expectedOrigins, int expectedDestinations)
        {
            jsonResult.Should().NotBeNullOrEmpty();
            
            var result = JsonDocument.Parse(jsonResult).RootElement;
            result.TryGetProperty("matrix", out var matrix).Should().BeTrue();
            
            matrix.ValueKind.Should().Be(JsonValueKind.Array);
            matrix.GetArrayLength().Should().Be(expectedOrigins);
            
            // Each origin should have entries for each destination
            for (int i = 0; i < matrix.GetArrayLength(); i++)
            {
                var originRow = matrix[i];
                originRow.ValueKind.Should().Be(JsonValueKind.Array);
                originRow.GetArrayLength().Should().Be(expectedDestinations);
                
                // Each destination entry should have response data
                for (int j = 0; j < originRow.GetArrayLength(); j++)
                {
                    var destination = originRow[j];
                    destination.TryGetProperty("response", out var response).Should().BeTrue();
                    response.TryGetProperty("routeSummary", out var routeSummary).Should().BeTrue();
                    routeSummary.TryGetProperty("lengthInMeters", out _).Should().BeTrue();
                    routeSummary.TryGetProperty("travelTimeInSeconds", out _).Should().BeTrue();
                }
            }
        }

        /// <summary>
        /// Validates a successful route range (isochrone) response
        /// </summary>
        public static void ValidateRouteRangeResponse(string jsonResult)
        {
            jsonResult.Should().NotBeNullOrEmpty();
            
            var result = JsonDocument.Parse(jsonResult).RootElement;
            result.TryGetProperty("formatVersion", out _).Should().BeTrue();
            result.TryGetProperty("reachableRange", out var reachableRange).Should().BeTrue();
            
            reachableRange.TryGetProperty("center", out var center).Should().BeTrue();
            reachableRange.TryGetProperty("boundary", out var boundary).Should().BeTrue();
            
            // Validate center coordinates
            center.TryGetProperty("latitude", out _).Should().BeTrue();
            center.TryGetProperty("longitude", out _).Should().BeTrue();
            
            // Validate boundary polygon
            boundary.ValueKind.Should().Be(JsonValueKind.Array);
            boundary.GetArrayLength().Should().BeGreaterThan(2); // At least 3 points for a polygon
        }

        /// <summary>
        /// Validates a successful route countries analysis response
        /// </summary>
        public static void ValidateRouteCountriesResponse(string jsonResult, int expectedMinCountries = 1)
        {
            jsonResult.Should().NotBeNullOrEmpty();
            
            var result = JsonDocument.Parse(jsonResult).RootElement;
            result.TryGetProperty("routeCountries", out var routeCountries).Should().BeTrue();
            result.TryGetProperty("waypointDetails", out var waypointDetails).Should().BeTrue();
            
            // Validate countries found
            routeCountries.ValueKind.Should().Be(JsonValueKind.Array);
            routeCountries.GetArrayLength().Should().BeGreaterThanOrEqualTo(expectedMinCountries);
            
            // Validate waypoint details
            waypointDetails.ValueKind.Should().Be(JsonValueKind.Array);
            waypointDetails.GetArrayLength().Should().BeGreaterThan(0);
            
            // Each waypoint should have coordinate and country information
            foreach (var waypoint in waypointDetails.EnumerateArray())
            {
                waypoint.TryGetProperty("coordinate", out var coordinate).Should().BeTrue();
                waypoint.TryGetProperty("country", out var country).Should().BeTrue();
                
                coordinate.TryGetProperty("latitude", out _).Should().BeTrue();
                coordinate.TryGetProperty("longitude", out _).Should().BeTrue();
                
                country.TryGetProperty("countryCode", out _).Should().BeTrue();
                country.TryGetProperty("countryName", out _).Should().BeTrue();
            }
        }
    }

    /// <summary>
    /// Validation methods for render responses
    /// </summary>
    public static class RenderValidation
    {
        /// <summary>
        /// Validates a successful static map image response
        /// </summary>
        public static void ValidateStaticMapImageResponse(string jsonResult)
        {
            jsonResult.Should().NotBeNullOrEmpty();
            
            var result = JsonDocument.Parse(jsonResult).RootElement;
            result.TryGetProperty("success", out var success).Should().BeTrue();
            success.GetBoolean().Should().BeTrue();
            
            result.TryGetProperty("result", out var resultData).Should().BeTrue();
            
            // Validate map info
            resultData.TryGetProperty("MapInfo", out var mapInfo).Should().BeTrue();
            mapInfo.TryGetProperty("BoundingBox", out var boundingBox).Should().BeTrue();
            mapInfo.TryGetProperty("ZoomLevel", out var zoomLevel).Should().BeTrue();
            mapInfo.TryGetProperty("Dimensions", out var dimensions).Should().BeTrue();
            mapInfo.TryGetProperty("Style", out var style).Should().BeTrue();
            mapInfo.TryGetProperty("MarkerCount", out var markerCount).Should().BeTrue();
            mapInfo.TryGetProperty("PathCount", out var pathCount).Should().BeTrue();
            
            // Validate bounding box structure
            boundingBox.TryGetProperty("West", out _).Should().BeTrue();
            boundingBox.TryGetProperty("South", out _).Should().BeTrue();
            boundingBox.TryGetProperty("East", out _).Should().BeTrue();
            boundingBox.TryGetProperty("North", out _).Should().BeTrue();
            
            // Validate dimensions
            dimensions.TryGetProperty("Width", out var width).Should().BeTrue();
            dimensions.TryGetProperty("Height", out var height).Should().BeTrue();
            width.GetInt32().Should().BeGreaterThan(0);
            height.GetInt32().Should().BeGreaterThan(0);
            
            // Validate zoom level
            var zoomValue = zoomLevel.GetInt32();
            zoomValue.Should().BeInRange(1, 20);
            
            // Validate image data
            resultData.TryGetProperty("ImageData", out var imageData).Should().BeTrue();
            imageData.TryGetProperty("Format", out var format).Should().BeTrue();
            imageData.TryGetProperty("SizeInBytes", out var sizeInBytes).Should().BeTrue();
            imageData.TryGetProperty("DataUri", out var dataUri).Should().BeTrue();
            
            format.GetString().Should().Be("PNG");
            sizeInBytes.GetInt32().Should().BeGreaterThan(0);
            dataUri.GetString().Should().StartWith("data:image/png;base64,");
        }

        /// <summary>
        /// Validates an error response for render operations
        /// </summary>
        public static void ValidateRenderErrorResponse(string jsonResult, string expectedErrorSubstring)
        {
            jsonResult.Should().NotBeNullOrEmpty();
            
            var result = JsonDocument.Parse(jsonResult).RootElement;
            result.TryGetProperty("error", out var error).Should().BeTrue();
            
            var errorMessage = error.GetString();
            errorMessage.Should().NotBeNullOrEmpty();
            errorMessage.Should().Contain(expectedErrorSubstring);
        }

        /// <summary>
        /// Validates that a bounding box contains required coordinate properties
        /// </summary>
        public static void ValidateBoundingBoxStructure(JsonElement boundingBox)
        {
            boundingBox.TryGetProperty("West", out var west).Should().BeTrue();
            boundingBox.TryGetProperty("South", out var south).Should().BeTrue();
            boundingBox.TryGetProperty("East", out var east).Should().BeTrue();
            boundingBox.TryGetProperty("North", out var north).Should().BeTrue();
            
            // Validate coordinate ranges
            var westValue = west.GetDouble();
            var southValue = south.GetDouble();
            var eastValue = east.GetDouble();
            var northValue = north.GetDouble();
            
            westValue.Should().BeInRange(-180, 180);
            southValue.Should().BeInRange(-90, 90);
            eastValue.Should().BeInRange(-180, 180);
            northValue.Should().BeInRange(-90, 90);
        }

        /// <summary>
        /// Validates that image dimensions are within acceptable ranges
        /// </summary>
        public static void ValidateImageDimensions(int width, int height)
        {
            width.Should().BeInRange(1, 8192, "Width must be between 1 and 8192 pixels");
            height.Should().BeInRange(1, 8192, "Height must be between 1 and 8192 pixels");
        }

        /// <summary>
        /// Validates that zoom level is within acceptable range
        /// </summary>
        public static void ValidateZoomLevel(int zoomLevel)
        {
            zoomLevel.Should().BeInRange(1, 20, "Zoom level must be between 1 and 20");
        }
    }
}
