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
            "Convert street addresses or place names to longitude and latitude coordinates. Returns detailed address properties including street, postal code, municipality, and country/region information."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "address",
            "string",
            "Address, partial address, or landmark name to geocode (e.g., '1600 Pennsylvania Avenue, Washington, DC' or 'Eiffel Tower')"
        )] string address,
        [McpToolProperty(
            "maxResults",
            "number",
            "Maximum number of results to return (1-100, default: 5)"
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

            maxResults = Math.Max(1, Math.Min(100, maxResults));

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
                    Confidence = feature.Properties.Confidence,
                    MatchCodes = feature.Properties.MatchCodes?.ToString()
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
            "Convert longitude and latitude coordinates to a street address and location details."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "latitude",
            "number",
            "Latitude coordinate (e.g., 47.6062)"
        )] double latitude,
        [McpToolProperty(
            "longitude",
            "number",
            "Longitude coordinate (e.g., -122.3321)"
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
                    MatchCodes = feature.Properties.MatchCodes?.ToString()
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
            "Get administrative boundary polygon (city, postal code, country subdivision, etc.) for a specific geographic location. Returns polygon coordinates that define the boundary."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "latitude",
            "number",
            "Latitude coordinate of the location to get boundary for (e.g., 47.61256)"
        )] double latitude,
        [McpToolProperty(
            "longitude",
            "number",
            "Longitude coordinate of the location to get boundary for (e.g., -122.204141)"
        )] double longitude,
        [McpToolProperty(
            "resultType",
            "string",
            "Type of boundary to retrieve: 'locality' (city), 'postalCode1' (postal code), 'adminDistrict1' (state/province), 'adminDistrict2' (county), 'countryRegion' (country)"
        )] string resultType = "locality",
        [McpToolProperty(
            "resolution",
            "string",
            "Level of detail for polygon coordinates: 'small' (fewer points), 'medium', 'large' (more detailed)"
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

            // Validate and convert result type
            if (!Enum.TryParse<BoundaryResultTypeEnum>(resultType, true, out var boundaryType))
            {
                return JsonSerializer.Serialize(new { error = $"Invalid result type '{resultType}'. Valid options: locality, postalCode1, adminDistrict1, adminDistrict2, countryRegion" });
            }

            // Validate and convert resolution
            if (!Enum.TryParse<ResolutionEnum>(resolution, true, out var resolutionLevel))
            {
                return JsonSerializer.Serialize(new { error = $"Invalid resolution '{resolution}'. Valid options: small, medium, large" });
            }

            logger.LogInformation("Getting polygon boundary for coordinates: {Latitude}, {Longitude} with type: {ResultType}", latitude, longitude, resultType);

            var options = new GetPolygonOptions()
            {
                Coordinates = new GeoPosition(longitude, latitude),
                ResultType = boundaryType,
                Resolution = resolutionLevel,
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