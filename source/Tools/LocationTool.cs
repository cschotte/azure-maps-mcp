// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using Azure.Maps.Search;
using Azure.Maps.Search.Models;
using Azure.Maps.Mcp.Services;
using Azure.Maps.Mcp.Common;
using Azure.Core.GeoJson;
using Azure.Maps.Mcp.Common.Models;

namespace Azure.Maps.Mcp.Tools;

/// <summary>
/// LocationTool - provides functionality for searching and analyzing locations
/// </summary>
public class LocationTool : BaseMapsTool
{
    private readonly MapsSearchClient _searchClient;

    private static readonly Dictionary<string, BoundaryResultTypeEnum> BoundaryTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "locality", BoundaryResultTypeEnum.Locality },
        { "postalcode", BoundaryResultTypeEnum.PostalCode },
        { "admindistrict", BoundaryResultTypeEnum.AdminDistrict },
        { "countryregion", BoundaryResultTypeEnum.CountryRegion }
    };

    private static readonly Dictionary<string, ResolutionEnum> Resolutions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "small", ResolutionEnum.Small },
        { "medium", ResolutionEnum.Medium },
        { "large", ResolutionEnum.Large }
    };

    public LocationTool(IAzureMapsService mapsService, ILogger<LocationTool> logger)
        : base(mapsService, logger)
    {
        _searchClient = mapsService.SearchClient;
    }

    /// <summary>
    /// Universal location search - handles both forward geocoding and location queries
    /// </summary>
    [Function(nameof(FindLocation))]
    public async Task<string> FindLocation(
        [McpToolTrigger(
            "location_find",
            "Find locations by address/place name. Returns coordinates, address components, and confidence scores."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "query",
            "string",
            "Address or place to search. Example: 'Eiffel Tower Paris' or '123 Main St Seattle WA'"
        )] string query,
        [McpToolProperty(
            "maxResults",
            "number",
            "Maximum results to return (1-20). Default: 5"
        )] int maxResults = 5,
        [McpToolProperty(
            "includeBoundaries",
            "boolean",
            "Include administrative boundaries. Default: false"
        )] bool includeBoundaries = false
    )
    {
        return await ExecuteWithErrorHandling(async () =>
        {
            // Unified validation
            var queryError = ValidateStringInput(query, 2, 2048, "Query");
            if (queryError != null) throw new ArgumentException(queryError);

            var (rangeError, normalizedMaxResults) = ValidateRange(maxResults, 1, 20, "maxResults");
            if (rangeError != null) throw new ArgumentException(rangeError);

            _logger.LogInformation("Location search: '{Query}' (max: {MaxResults}, boundaries: {Boundaries})",
                query.Trim(), normalizedMaxResults, includeBoundaries);

            // Perform geocoding
            var geocodingOptions = new GeocodingQuery() { Query = query.Trim(), Top = normalizedMaxResults };
            var response = await _searchClient.GetGeocodingAsync(query: query.Trim(), options: geocodingOptions);

            if (response.Value?.Features == null || !response.Value.Features.Any())
            {
                return new { query = query.Trim(), results = Array.Empty<object>(), count = 0 };
            }

            var results = new List<object>();

            foreach (var feature in response.Value.Features)
            {
                var location = new
                {
                    address = feature.Properties.Address?.FormattedAddress,
                    coordinates = new
                    {
                        latitude = feature.Geometry.Coordinates[1],
                        longitude = feature.Geometry.Coordinates[0]
                    },
                    components = new
                    {
                        streetNumber = feature.Properties.Address?.StreetNumber,
                        streetName = feature.Properties.Address?.StreetName,
                        locality = feature.Properties.Address?.Locality,
                        postalCode = feature.Properties.Address?.PostalCode,
                        country = feature.Properties.Address?.CountryRegion?.Name,
                        countryCode = feature.Properties.Address?.CountryRegion?.Iso
                    },
                    confidence = feature.Properties.Confidence.ToString()
                };

                // Optionally add boundary information
                if (includeBoundaries)
                {
                    try
                    {
                        var lat = feature.Geometry.Coordinates[1];
                        var lon = feature.Geometry.Coordinates[0];
                        var boundaryResult = await GetLocationBoundary(lat, lon, "locality", "small");

                        results.Add(new
                        {
                            location,
                            boundary = boundaryResult
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get boundary for location");
                        results.Add(new { location, boundary = (object?)null });
                    }
                }
                else
                {
                    results.Add(location);
                }
            }

            return new
            {
                query = query.Trim(),
                results,
                count = results.Count,
        includedBoundaries = includeBoundaries
            };

    }, "FindLocation", new { query, maxResults, includeBoundaries });
    }

    /// <summary>
    /// Reverse geocoding and boundary analysis from coordinates
    /// </summary>
    [Function(nameof(AnalyzeLocation))]
    public async Task<string> AnalyzeLocation(
        [McpToolTrigger(
            "location_analyze",
            "Analyze coordinates to get address, boundaries, and administrative context."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "latitude",
            "number",
            "Latitude coordinate (-90 to 90). Example: 47.6062"
        )] double latitude,
        [McpToolProperty(
            "longitude",
            "number",
            "Longitude coordinate (-180 to 180). Example: -122.3321"
        )] double longitude,
        [McpToolProperty(
            "boundaryType",
            "string",
            "Boundary level: locality, postalCode, adminDistrict, or countryRegion. Default: locality"
        )] string boundaryType = "locality",
        [McpToolProperty(
            "resolution",
            "string",
            "Boundary resolution: small, medium, or large. Default: small"
        )] string resolution = "small"
    )
    {
        return await ExecuteWithErrorHandling(async () =>
        {
            // Validate coordinates
            var coordError = ValidateCoordinates(latitude, longitude);
            if (coordError != null) throw new ArgumentException(coordError);

            // Validate boundary type
            if (!BoundaryTypes.TryGetValue(boundaryType, out var boundaryEnum))
            {
                var validOptions = string.Join(", ", BoundaryTypes.Keys);
                throw new ArgumentException($"Invalid boundary type '{boundaryType}'. Valid options: {validOptions}");
            }

            // Validate resolution
            if (!Resolutions.TryGetValue(resolution, out var resolutionEnum))
            {
                var validOptions = string.Join(", ", Resolutions.Keys);
                throw new ArgumentException($"Invalid resolution '{resolution}'. Valid options: {validOptions}");
            }

            _logger.LogInformation("Analyzing location: {Latitude}, {Longitude} (boundary: {BoundaryType}, resolution: {Resolution})",
                latitude, longitude, boundaryType, resolution);

            // Perform reverse geocoding
            var coordinates = new GeoPosition(longitude, latitude);
            var reverseResponse = await _searchClient.GetReverseGeocodingAsync(coordinates);

            object? addressInfo = null;
            if (reverseResponse.Value?.Features?.Any() == true)
            {
                var feature = reverseResponse.Value.Features.First();
                var address = feature.Properties.Address;
                addressInfo = new
                {
                    formatted = address?.FormattedAddress,
                    components = new
                    {
                        streetNumber = address?.StreetNumber,
                        streetName = address?.StreetName,
                        locality = address?.Locality,
                        postalCode = address?.PostalCode,
                        country = address?.CountryRegion?.Name,
                        countryCode = address?.CountryRegion?.Iso
                    }
                };
            }

            // Get boundary information
            var boundary = await GetLocationBoundary(latitude, longitude, boundaryType, resolution);

            return new
            {
                coordinates = new { latitude, longitude },
                address = addressInfo,
                boundary = new
                {
                    type = boundaryType,
                    resolution,
                    geometry = boundary
                }
            };

        }, "AnalyzeLocation", new { latitude, longitude, boundaryType, resolution });
    }

    /// <summary>
    /// Helper method to get boundary information
    /// </summary>
    private async Task<object?> GetLocationBoundary(double latitude, double longitude, string boundaryType, string resolution)
    {
        try
        {
            var options = new GetPolygonOptions()
            {
                Coordinates = new GeoPosition(longitude, latitude),
                ResultType = BoundaryTypes[boundaryType],
                Resolution = Resolutions[resolution]
            };

            var response = await _searchClient.GetPolygonAsync(options);

            if (response.Value?.Geometry != null && response.Value.Geometry.Count > 0)
            {
                var geometries = new List<object>();
                for (int i = 0; i < response.Value.Geometry.Count; i++)
                {
                    if (response.Value.Geometry[i] is GeoPolygon polygon)
                    {
                        var coords = polygon.Coordinates[0].Select(c => new[] { c.Latitude, c.Longitude }).ToArray();
                        geometries.Add(new { index = i, pointCount = coords.Length, coordinates = coords });
                    }
                }

                return new
                {
                    count = geometries.Count,
                    polygons = geometries,
                    copyright = response.Value.Properties?.Copyright
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get boundary for coordinates: {Latitude}, {Longitude}", latitude, longitude);
        }

        return null;
    }

}