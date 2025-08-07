// Copyright (c) 2025 Clemens Schotte
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using Azure.Maps.Search;
using Azure.Maps.Search.Models;
using Azure.Maps.Mcp.Services;
using Azure.Core.GeoJson;
using System.Text.Json;

namespace Azure.Maps.Mcp.Tools;

/// <summary>
/// Azure Maps Search Tool providing geocoding, reverse geocoding, and administrative boundary polygon capabilities
/// </summary>
public class SearchTool(IAzureMapsService azureMapsService, ILogger<SearchTool> logger)
{
    private readonly MapsSearchClient _searchClient = azureMapsService.SearchClient;

    /// <summary>
    /// Converts an address or place name to geographic coordinates
    /// </summary>
    [Function(nameof(Geocoding))]
    public async Task<string> Geocoding(
        [McpToolTrigger(
            "geocoding",
            "Convert street addresses, landmarks, or place names into precise geographic coordinates (latitude and longitude). This forward geocoding service handles various address formats from complete street addresses to partial addresses or landmark names. Returns detailed address components including street details, postal codes, administrative areas, and confidence scores. Essential for mapping applications, location-based services, and spatial analysis."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "address",
            "string",
            "Address, partial address, or landmark name to geocode (e.g., '1600 Pennsylvania Avenue, Washington, DC' or 'Eiffel Tower')"
        )] string address,
        [McpToolProperty(
            "maxResults",
            "string",
            "Maximum number of results to return as a string number (e.g., '5'). Must be between 1 and 20. Default is '5' if not specified."
        )] int maxResults = 5
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                logger.LogWarning("Empty address provided for geocoding");
                return JsonSerializer.Serialize(new { error = "Address is required" });
            }

            maxResults = Math.Max(1, Math.Min(20, maxResults));

            logger.LogInformation("Geocoding address: {Address}", address);

            var options = new GeocodingQuery() { Query = address, Top = maxResults };

            var response = await _searchClient.GetGeocodingAsync(query: address, options: options);

            if (response.Value?.Features != null)
            {
                var results = response.Value.Features.Select(feature => new
                {
                    Address = feature.Properties.Address?.FormattedAddress,
                    Coordinates = new
                    {
                        Longitude = feature.Geometry.Coordinates[0],
                        Latitude = feature.Geometry.Coordinates[1]
                    },
                    AddressDetails = new
                    {
                        StreetNumber = feature.Properties.Address?.StreetNumber,
                        StreetName = feature.Properties.Address?.StreetName,
                        Neighborhood = feature.Properties.Address?.Neighborhood,
                        PostalCode = feature.Properties.Address?.PostalCode,
                        CountryRegion = feature.Properties.Address?.CountryRegion,
                        Locality = feature.Properties.Address?.Locality
                    },
                    Confidence = feature.Properties.Confidence.ToString(),
                    MatchCodes = feature.Properties.MatchCodes?.Select(mc => mc.ToString()).ToArray()
                });

                logger.LogInformation("Successfully geocoded address, found {Count} results", results.Count());
                return JsonSerializer.Serialize(new { success = true, results });
            }

            logger.LogWarning("No results found for address: {Address}", address);
            return JsonSerializer.Serialize(new { success = false, message = "No results found" });
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Maps API error during geocoding: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = $"API Error: {ex.Message}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during geocoding");
            return JsonSerializer.Serialize(new { error = "An unexpected error occurred" });
        }
    }

    /// <summary>
    /// Converts geographic coordinates to a street address
    /// </summary>
    [Function(nameof(ReverseGeocoding))]
    public async Task<string> ReverseGeocoding(
        [McpToolTrigger(
            "reverse_geocoding",
            "Convert precise geographic coordinates (latitude and longitude) into human-readable street addresses and location details. This reverse geocoding service is essential for location-based applications that need to display meaningful address information from GPS coordinates or map click events. Returns formatted addresses with detailed components including street names, postal codes, and administrative boundaries."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "latitude",
            "string",
            "Latitude coordinate as a decimal number (e.g., '47.6062'). Must be between -90 and 90 degrees."
        )] double latitude,
        [McpToolProperty(
            "longitude",
            "string",
            "Longitude coordinate as a decimal number (e.g., '-122.3321'). Must be between -180 and 180 degrees."
        )] double longitude
    )
    {
        try
        {
            if (latitude < -90 || latitude > 90)
            {
                return JsonSerializer.Serialize(new { error = "Latitude must be between -90 and 90 degrees" });
            }

            if (longitude < -180 || longitude > 180)
            {
                return JsonSerializer.Serialize(new { error = "Longitude must be between -180 and 180 degrees" });
            }

            logger.LogInformation("Reverse geocoding coordinates: {Latitude}, {Longitude}", latitude, longitude);

            var coordinates = new GeoPosition(longitude, latitude);
            var response = await _searchClient.GetReverseGeocodingAsync(coordinates);

            if (response.Value?.Features != null && response.Value.Features.Any())
            {
                var feature = response.Value.Features.First();
                var result = new
                {
                    Address = feature.Properties.Address?.FormattedAddress,
                    AddressDetails = new
                    {
                        StreetNumber = feature.Properties.Address?.StreetNumber,
                        StreetName = feature.Properties.Address?.StreetName,
                        Neighborhood = feature.Properties.Address?.Neighborhood,
                        PostalCode = feature.Properties.Address?.PostalCode,
                        CountryRegion = feature.Properties.Address?.CountryRegion,
                        Locality = feature.Properties.Address?.Locality
                    },
                    Coordinates = new
                    {
                        Latitude = latitude,
                        Longitude = longitude
                    },
                    MatchCodes = feature.Properties.MatchCodes?.Select(mc => mc.ToString()).ToArray()
                };

                logger.LogInformation("Successfully reverse geocoded coordinates");
                return JsonSerializer.Serialize(new { success = true, result });
            }

            logger.LogWarning("No address found for coordinates: {Latitude}, {Longitude}", latitude, longitude);
            return JsonSerializer.Serialize(new { success = false, message = "No address found for these coordinates" });
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Maps API error during reverse geocoding: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = $"API Error: {ex.Message}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during reverse geocoding");
            return JsonSerializer.Serialize(new { error = "An unexpected error occurred" });
        }
    }
    
    /// <summary>
    /// Gets administrative boundary polygon for a specific location
    /// </summary>
    [Function(nameof(GetPolygon))]
    public async Task<string> GetPolygon(
        [McpToolTrigger(
            "get_polygon",
            "Retrieve administrative boundary polygons for geographic locations such as city limits, postal code areas, state/province boundaries, or country borders. This service returns precise polygon coordinates that define these administrative boundaries, enabling spatial analysis, territory mapping, and geofencing applications. Essential for analyzing geographic containment, service area definition, and administrative boundary visualization."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "latitude",
            "string",
            "Latitude coordinate of the location to get boundary polygon for as a decimal number (e.g., '47.61256'). Must be between -90 and 90 degrees."
        )] double latitude,
        [McpToolProperty(
            "longitude",
            "string",
            "Longitude coordinate of the location to get boundary polygon for as a decimal number (e.g., '-122.204141'). Must be between -180 and 180 degrees."
        )] double longitude,
        [McpToolProperty(
            "resultType",
            "string",
            "Type of administrative boundary to retrieve: 'locality' (city/town boundaries), 'postalCode' (postal/ZIP code boundaries), 'adminDistrict' (state/province boundaries), 'countryRegion' (country boundaries). Default is 'locality'."
        )] string resultType = "locality",
        [McpToolProperty(
            "resolution",
            "string",
            "Level of detail for polygon coordinates: 'small' (fewer coordinate points, faster), 'medium' (balanced detail), 'large' (highly detailed boundaries, more points). Default is 'small'."
        )] string resolution = "small"
    )
    {
        try
        {
            if (latitude < -90 || latitude > 90)
            {
                return JsonSerializer.Serialize(new { error = "Latitude must be between -90 and 90 degrees" });
            }

            if (longitude < -180 || longitude > 180)
            {
                return JsonSerializer.Serialize(new { error = "Longitude must be between -180 and 180 degrees" });
            }

            // Validate result type options
            var validResultTypes = new Dictionary<string, BoundaryResultTypeEnum>(StringComparer.OrdinalIgnoreCase)
            {
                { "locality", BoundaryResultTypeEnum.Locality },
                { "postalcode", BoundaryResultTypeEnum.PostalCode },
                { "admindistrict", BoundaryResultTypeEnum.AdminDistrict },
                { "countryregion", BoundaryResultTypeEnum.CountryRegion }
            };

            if (!validResultTypes.TryGetValue(resultType, out var resultTypeEnum))
            {
                var validOptions = string.Join(", ", validResultTypes.Keys);
                return JsonSerializer.Serialize(new { error = $"Invalid result type '{resultType}'. Valid options: {validOptions}" });
            }

            // Validate resolution options
            var validResolutions = new Dictionary<string, ResolutionEnum>(StringComparer.OrdinalIgnoreCase)
            {
                { "small", ResolutionEnum.Small },
                { "medium", ResolutionEnum.Medium },
                { "large", ResolutionEnum.Large }
            };

            if (!validResolutions.TryGetValue(resolution, out var resolutionEnum))
            {
                var validOptions = string.Join(", ", validResolutions.Keys);
                return JsonSerializer.Serialize(new { error = $"Invalid resolution '{resolution}'. Valid options: {validOptions}" });
            }

            logger.LogInformation("Getting polygon boundary for coordinates: {Latitude}, {Longitude} with type: {ResultType}", latitude, longitude, resultType);

            var options = new GetPolygonOptions()
            {
                Coordinates = new GeoPosition(longitude, latitude),
                ResultType = resultTypeEnum,
                Resolution = resolutionEnum
            };

            var response = await _searchClient.GetPolygonAsync(options);

            if (response.Value?.Geometry != null && response.Value.Geometry.Count > 0)
            {
                var polygons = new List<object>();

                for (int i = 0; i < response.Value.Geometry.Count; i++)
                {
                    if (response.Value.Geometry[i] is GeoPolygon polygon)
                    {
                        var coordinates = polygon.Coordinates[0].Select(coord => new
                        {
                            Latitude = coord.Latitude,
                            Longitude = coord.Longitude
                        }).ToArray();

                        polygons.Add(new
                        {
                            PolygonIndex = i,
                            CoordinateCount = coordinates.Length,
                            Coordinates = coordinates
                        });
                    }
                }

                var result = new
                {
                    BoundaryInfo = new
                    {
                        CopyrightUrl = response.Value.Properties?.CopyrightUrl,
                        Copyright = response.Value.Properties?.Copyright,
                        ResultType = resultType,
                        Resolution = resolution,
                        QueryCoordinates = new
                        {
                            Latitude = latitude,
                            Longitude = longitude
                        }
                    },
                    PolygonCount = response.Value.Geometry.Count,
                    Polygons = polygons
                };

                logger.LogInformation("Successfully retrieved {Count} polygon(s) for boundary", response.Value.Geometry.Count);
                return JsonSerializer.Serialize(new { success = true, result }, new JsonSerializerOptions { WriteIndented = true });
            }

            logger.LogWarning("No boundary polygon found for coordinates: {Latitude}, {Longitude}", latitude, longitude);
            return JsonSerializer.Serialize(new { success = false, message = "No boundary polygon found for these coordinates" });
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Maps API error during polygon retrieval: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = $"API Error: {ex.Message}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during polygon retrieval");
            return JsonSerializer.Serialize(new { error = "An unexpected error occurred" });
        }
    }
}