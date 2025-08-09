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
using System.Text.Json;

namespace Azure.Maps.Mcp.Tools;

/// <summary>
/// Azure Maps Search Tool providing geocoding, reverse geocoding, and administrative boundary polygon capabilities
/// </summary>
public class SearchTool(IAzureMapsService azureMapsService, ILogger<SearchTool> logger)
{
    private readonly MapsSearchClient _searchClient = azureMapsService.SearchClient;
    private static readonly Dictionary<string, BoundaryResultTypeEnum> ResultTypes = new(StringComparer.OrdinalIgnoreCase)
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

    /// <summary>
    /// Forward geocoding: address/place -> coordinates
    /// </summary>
    [Function(nameof(Geocoding))]
    public async Task<string> Geocoding(
        [McpToolTrigger(
            "search_geocoding",
            "Forward geocoding. Convert address/place to coordinates with confidence and components. Example: 'Eiffel Tower Paris' -> lat/lon."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "address",
            "string",
            "Address or place text. Examples: '123 Main St Seattle WA', 'Eiffel Tower Paris', 'JFK Airport NYC'."
        )] string address,
        [McpToolProperty(
            "maxResults",
            "number",
            "1..20 (default 5). Higher values return more candidates."
        )] int maxResults = 5
    )
    {
        try
        {
            // Validate input
            var addressValidation = ValidationHelper.ValidateStringInput(address, 2, 2048, "Address");
            if (!addressValidation.IsValid)
                return ResponseHelper.CreateErrorResponse(addressValidation.ErrorMessage!);

            var rangeValidation = ValidationHelper.ValidateRange(maxResults, 1, 20, "maxResults");
            maxResults = rangeValidation.NormalizedValue;

            var normalizedAddress = address.Trim();
            logger.LogInformation("Geocoding address: '{Address}' (requesting {MaxResults} results)", normalizedAddress, maxResults);

            var options = new GeocodingQuery() { Query = normalizedAddress, Top = maxResults };
            var response = await _searchClient.GetGeocodingAsync(query: normalizedAddress, options: options);

            if (response.Value?.Features != null && response.Value.Features.Any())
            {
                var locations = response.Value.Features.Select(f => new
                {
                    address = f.Properties.Address?.FormattedAddress,
                    coordinates = new { latitude = f.Geometry.Coordinates[1], longitude = f.Geometry.Coordinates[0] },
                    components = new
                    {
                        streetNumber = f.Properties.Address?.StreetNumber,
                        streetName = f.Properties.Address?.StreetName,
                        locality = f.Properties.Address?.Locality,
                        postalCode = f.Properties.Address?.PostalCode,
                        country = f.Properties.Address?.CountryRegion?.Name,
                        countryCode = f.Properties.Address?.CountryRegion?.Iso
                    },
                    confidence = f.Properties.Confidence.ToString()
                }).ToList();

                logger.LogInformation("Geocode ok: {Count} results", locations.Count);
                return ResponseHelper.CreateSuccessResponse(new { query = normalizedAddress, results = locations });
            }

            // No results found - AI-optimized error response
            return ResponseHelper.CreateErrorResponse($"No locations found for '{normalizedAddress}'");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during geocoding for address: {Address}", address);
            
            return ResponseHelper.CreateErrorResponse("Geocoding service error", new { ex.Message, address });
        }
    }

    // string similarity and code conversion helpers centralized in ToolsHelper

    /// <summary>
    /// Converts geographic coordinates back to a human-readable address (reverse geocoding)
    /// </summary>
    [Function(nameof(ReverseGeocoding))]
    public async Task<string> ReverseGeocoding(
        [McpToolTrigger(
            "search_geocoding_reverse",
            "Reverse geocoding. Convert coordinates to address and context."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "latitude",
            "number",
            "-90..90. Example: 47.6062"
        )] double latitude,
        [McpToolProperty(
            "longitude",
            "number",
            "-180..180. Example: -122.3321"
        )] double longitude
    )
    {
        try
        {
            // Validate coordinates
            var validation = ValidationHelper.ValidateCoordinates(latitude, longitude);
            if (!validation.IsValid)
                return ResponseHelper.CreateErrorResponse(validation.ErrorMessage!);

            logger.LogInformation("Reverse geocoding coordinates: {Latitude}, {Longitude}", latitude, longitude);

            var coordinates = new GeoPosition(longitude, latitude);
            var response = await _searchClient.GetReverseGeocodingAsync(coordinates);

            if (response.Value?.Features?.Any() == true)
            {
                var f = response.Value.Features.First();
                var address = f.Properties.Address;
                var result = new
                {
                    address = new
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
                    },
                    coordinates = new { latitude, longitude }
                };

                logger.LogInformation("Reverse geocode ok");
                return ResponseHelper.CreateSuccessResponse(result);
            }

            // No results found
            return ResponseHelper.CreateErrorResponse($"No address found for coordinates ({latitude}, {longitude})");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during reverse geocoding");
            
            return ResponseHelper.CreateErrorResponse("Reverse geocoding service error", new { ex.Message, latitude, longitude });
        }
    }
    
    /// <summary>
    /// Gets administrative boundary polygon for a specific location
    /// </summary>
    [Function(nameof(GetPolygon))]
    public async Task<string> GetPolygon(
        [McpToolTrigger(
            "search_polygon",
            "Administrative boundary polygon at a point."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "latitude",
            "number",
            "-90..90"
        )] double latitude,
        [McpToolProperty(
            "longitude",
            "number",
            "-180..180"
        )] double longitude,
        [McpToolProperty(
            "resultType",
            "string",
            "locality|postalCode|adminDistrict|countryRegion (default locality)"
        )] string resultType = "locality",
        [McpToolProperty(
            "resolution",
            "string",
            "small|medium|large (default small)"
        )] string resolution = "small"
    )
    {
        try
        {
            var coordValidation = ValidationHelper.ValidateCoordinates(latitude, longitude);
            if (!coordValidation.IsValid)
                return JsonSerializer.Serialize(new { error = coordValidation.ErrorMessage });

            // Validate result type options
            if (!ResultTypes.TryGetValue(resultType, out var resultTypeEnum))
            {
                var validOptions = string.Join(", ", ResultTypes.Keys);
                return JsonSerializer.Serialize(new { error = $"Invalid result type '{resultType}'. Valid options: {validOptions}" });
            }

            // Validate resolution options
            if (!Resolutions.TryGetValue(resolution, out var resolutionEnum))
            {
                var validOptions = string.Join(", ", Resolutions.Keys);
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
                var geoms = new List<object>();
                for (int i = 0; i < response.Value.Geometry.Count; i++)
                {
                    if (response.Value.Geometry[i] is GeoPolygon polygon)
                    {
                        var coords = polygon.Coordinates[0].Select(c => new[] { c.Latitude, c.Longitude }).ToArray();
                        geoms.Add(new { i, n = coords.Length, coords });
                    }
                }

                var meta = new
                {
                    type = resultType,
                    res = resolution,
                    lat = latitude,
                    lon = longitude,
                    cr = response.Value.Properties?.Copyright
                };

                var result = new { meta, count = geoms.Count, geometries = geoms };
                logger.LogInformation("Boundary ok: {Count}", geoms.Count);
                return ResponseHelper.CreateSuccessResponse(result);
            }

            logger.LogWarning("No boundary polygon found for coordinates: {Latitude}, {Longitude}", latitude, longitude);
            return ResponseHelper.CreateErrorResponse("No boundary polygon found for these coordinates");
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Maps API error during polygon retrieval: {Message}", ex.Message);
            return ResponseHelper.CreateErrorResponse($"API Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during polygon retrieval");
            return ResponseHelper.CreateErrorResponse("An unexpected error occurred");
        }
    }

    // Country endpoints moved to CountryTool to reduce file size and complexity
    /// <summary>
    /// Country-specific helpers removed
    /// </summary>

    #region Helper Methods (kept minimal)

    private static string DetermineLocationType(Address? address)
    {
        if (address?.StreetNumber != null && address?.StreetName != null)
            return "STREET_ADDRESS";
        if (address?.Locality != null && address?.StreetName == null)
            return "CITY";
        if (address?.CountryRegion != null && address?.Locality == null)
            return "COUNTRY";
        return "LANDMARK";
    }

    private static double CalculateQualityScore(ConfidenceEnum? confidence, string? formattedAddress)
    {
        var baseScore = confidence?.ToString().ToLowerInvariant() switch
        {
            "high" => 0.9,
            "medium" => 0.6,
            "low" => 0.3,
            _ => 0.1
        };

        // Boost score for more complete addresses
        if (!string.IsNullOrEmpty(formattedAddress))
        {
            var parts = formattedAddress.Split(',').Length;
            baseScore += Math.Min(0.1, parts * 0.02);
        }

        return Math.Min(1.0, baseScore);
    }

    // Usage hints removed

    // Query quality scoring removed

    // Confidence parsing removed

    // Optimization tips removed

    // Query suggestions removed

    // Geographic context helpers removed for simplicity

    // Address completeness removed for simplicity

    #endregion
}