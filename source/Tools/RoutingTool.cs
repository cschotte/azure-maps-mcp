// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using Azure.Core.GeoJson;
using Azure.Maps.Mcp.Services;
using Azure.Maps.Routing;
using System.Text.Json;
using CountryData.Standard;
using Azure.Maps.Mcp.Common;
using Azure.Maps.Mcp.Common.Models;

namespace Azure.Maps.Mcp.Tools;

// Replaced by Common.Models.LatLon

/// <summary>
/// Azure Maps Routing Tool providing route directions, route matrix, and route range capabilities
/// </summary>
public class RoutingTool(IAzureMapsService azureMapsService, ILogger<RoutingTool> logger, CountryHelper countryHelper)
{
    private readonly MapsRoutingClient _routingClient = azureMapsService.RoutingClient;
    private readonly CountryHelper _countryHelper = countryHelper;
    
    // Validation centralizes in ValidationHelper

    /// <summary>
    /// Calculate route directions between coordinates
    /// </summary>
    [Function(nameof(GetRouteDirections))]
    public async Task<string> GetRouteDirections(
        [McpToolTrigger(
            "routing_directions",
            "Directions between 2+ points with traffic. Returns distance, ETA, geometry, and instructions."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "coordinates",
            "array",
            "Array of {latitude,longitude}. Min 2 points. Example: [{latitude:47.6062,longitude:-122.3321},{latitude:47.6205,longitude:-122.3493}]."
    )] LatLon[] coordinates,
        [McpToolProperty(
            "travelMode",
            "string",
            "car|truck|taxi|bus|van|motorcycle|bicycle|pedestrian (default car)"
        )] string travelMode = "car",
        [McpToolProperty(
            "routeType",
            "string",
            "fastest|shortest (default fastest)"
        )] string routeType = "fastest",
        [McpToolProperty(
            "avoidTolls",
            "string",
            "true|false (default false)"
        )] string avoidTolls = "false",
        [McpToolProperty(
            "avoidHighways",
            "string",
            "true|false (default false)"
        )] string avoidHighways = "false"
    )
    {
        try
        {
            var coordsValidation = ValidationHelper.ValidateCoordinateArray(coordinates, c => (c.Latitude, c.Longitude), 2);
            if (!coordsValidation.IsValid)
                return ResponseHelper.CreateErrorResponse(coordsValidation.ErrorMessage!);

            var routePoints = coordinates
                .Select(c => new GeoPosition(c.Longitude, c.Latitude))
                .ToList();

            logger.LogInformation("Calculating route directions for {Count} points", routePoints.Count);

            // Validate travel mode options
            var tmParsed = ToolsHelper.ParseTravelMode(travelMode);
            if (!tmParsed.IsValid) return ResponseHelper.CreateErrorResponse(tmParsed.Error!);
            var parsedTravelMode = tmParsed.Value;

            // Validate route type options
            var rtParsed = ToolsHelper.ParseRouteType(routeType);
            if (!rtParsed.IsValid) return ResponseHelper.CreateErrorResponse(rtParsed.Error!);
            var parsedRouteType = rtParsed.Value;

            // Parse boolean parameters using shared validator
            var avoidTollsParse = ValidationHelper.ValidateBooleanString(avoidTolls, nameof(avoidTolls));
            if (!avoidTollsParse.IsValid)
                return ResponseHelper.CreateErrorResponse(avoidTollsParse.ErrorMessage!);

            var avoidHighwaysParse = ValidationHelper.ValidateBooleanString(avoidHighways, nameof(avoidHighways));
            if (!avoidHighwaysParse.IsValid)
                return ResponseHelper.CreateErrorResponse(avoidHighwaysParse.ErrorMessage!);

            var options = new RouteDirectionOptions()
            {
                TravelMode = parsedTravelMode,
                RouteType = parsedRouteType,
                UseTrafficData = true,
                ComputeBestWaypointOrder = routePoints.Count > 2,
                InstructionsType = RouteInstructionsType.Text
            };

            // Configure avoidance options
            if (avoidTollsParse.Value) options.Avoid.Add(RouteAvoidType.TollRoads);
            if (avoidHighwaysParse.Value) options.Avoid.Add(RouteAvoidType.Motorways);

            var routeQuery = new RouteDirectionQuery(routePoints, options);
            var response = await _routingClient.GetDirectionsAsync(routeQuery);

            if (response.Value?.Routes != null && response.Value.Routes.Any())
            {
                var route = response.Value.Routes.First();
                var dist_m = route.Summary.LengthInMeters;
                var time_s = route.Summary.TravelTimeDuration?.TotalSeconds;
                var geom = route.Legs?.SelectMany(leg => leg.Points ?? new List<GeoPosition>())
                    .Select(p => new[] { p.Latitude, p.Longitude }).ToList();
                var legs = route.Legs?.Select(leg => new
                {
                    dist_m = leg.Summary.LengthInMeters,
                    time_s = leg.Summary.TravelTimeInSeconds,
                    delay_s = leg.Summary.TrafficDelayInSeconds
                }).ToList();

                var instructions = route.Guidance?.Instructions?.Select(i => new
                {
                    message = i.Message,
                    offsetMeters = i.RouteOffsetInMeters,
                    travelTimeSeconds = i.TravelTimeInSeconds,
                    maneuver = i.Maneuver?.ToString(),
                    turnAngleDegrees = i.TurnAngleInDegrees
                }).ToList();

                var result = new
                {
                    summary = new
                    {
                        distanceMeters = dist_m,
                        travelTimeSeconds = time_s,
                        trafficDelaySeconds = route.Summary.TravelTimeDuration?.TotalSeconds
                    },
                    geometry = route.Legs?.SelectMany(leg => leg.Points ?? new List<GeoPosition>())
                        .Select(p => new { latitude = p.Latitude, longitude = p.Longitude }).ToList(),
                    legs,
                    instructions
                };

                logger.LogInformation("Route ok");
                return ResponseHelper.CreateSuccessResponse(result);
            }

            // No route found
            logger.LogWarning("No route found between the specified coordinates");
            return ResponseHelper.CreateErrorResponse("No route found between the specified coordinates");
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Maps API error during route calculation: {Message}", ex.Message);
            
            return ResponseHelper.CreateErrorResponse("Azure Maps routing service error", new { ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during route calculation");
            return ResponseHelper.CreateErrorResponse("An unexpected error occurred");
        }
    }

    /// <summary>
    /// Calculate travel times and distances between multiple origins and destinations
    /// </summary>
    [Function(nameof(GetRouteMatrix))]
    public async Task<string> GetRouteMatrix(
        [McpToolTrigger(
            "routing_matrix",
            "Travel time/distance matrix for origins x destinations."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "origins",
            "array",
            "Array of {latitude,longitude}."
    )] LatLon[] origins,
        [McpToolProperty(
            "destinations",
            "array",
            "Array of {latitude,longitude}."
    )] LatLon[] destinations,
        [McpToolProperty(
            "travelMode",
            "string",
            "car|truck|taxi|bus|van|motorcycle|bicycle|pedestrian"
        )] string travelMode = "car",
        [McpToolProperty(
            "routeType",
            "string",
            "fastest|shortest"
        )] string routeType = "fastest"
    )
    {
        try
        {
            var originValidation = ValidationHelper.ValidateCoordinateArray(origins, c => (c.Latitude, c.Longitude), 1);
            if (!originValidation.IsValid)
                return ResponseHelper.CreateErrorResponse(originValidation.ErrorMessage!);

            var destValidation = ValidationHelper.ValidateCoordinateArray(destinations, c => (c.Latitude, c.Longitude), 1);
            if (!destValidation.IsValid)
                return ResponseHelper.CreateErrorResponse(destValidation.ErrorMessage!);

            // Convert to GeoPosition lists
            var originPoints = origins.Select(c => new GeoPosition(c.Longitude, c.Latitude)).ToList();
            var destinationPoints = destinations.Select(c => new GeoPosition(c.Longitude, c.Latitude)).ToList();

            logger.LogInformation("Calculating route matrix for {OriginCount} origins and {DestinationCount} destinations", 
                originPoints.Count, destinationPoints.Count);

            // Validate travel mode options
            var tmParsed = ToolsHelper.ParseTravelMode(travelMode);
            if (!tmParsed.IsValid) return ResponseHelper.CreateErrorResponse(tmParsed.Error!);
            var parsedTravelMode = tmParsed.Value;

            // Validate route type options
            var rtParsed = ToolsHelper.ParseRouteType(routeType);
            if (!rtParsed.IsValid) return ResponseHelper.CreateErrorResponse(rtParsed.Error!);
            var parsedRouteType = rtParsed.Value;

            var matrixQuery = new RouteMatrixQuery
            {
                Origins = originPoints,
                Destinations = destinationPoints
            };

            var options = new RouteMatrixOptions(matrixQuery)
            {
                TravelMode = parsedTravelMode,
                RouteType = parsedRouteType,
                UseTrafficData = true
            };

            var response = await _routingClient.GetImmediateRouteMatrixAsync(options);

            if (response.Value?.Matrix != null)
            {
                var matrix = response.Value.Matrix;
                var items = new List<object>(matrix.Count * (matrix.FirstOrDefault()?.Count ?? 1));
                int okCount = 0;

                for (int i = 0; i < matrix.Count; i++)
                {
                    var row = matrix[i];
                    for (int j = 0; j < row.Count; j++)
                    {
                        var cell = row[j];
                        if (cell.Summary != null)
                        {
                            okCount++;
                            items.Add(new
                            {
                                i,
                                j,
                                o = new[] { originPoints[i].Latitude, originPoints[i].Longitude },
                                d = new[] { destinationPoints[j].Latitude, destinationPoints[j].Longitude },
                                dist_m = cell.Summary.LengthInMeters,
                                time_s = cell.Summary.TravelTimeInSeconds,
                                delay_s = cell.Summary.TrafficDelayInSeconds
                            });
                        }
                        else
                        {
                            items.Add(new { i, j, err = "not_found" });
                        }
                    }
                }

                var result = new
                {
                    oc = originPoints.Count,
                    dc = destinationPoints.Count,
                    total = items.Count,
                    ok = okCount,
                    fail = items.Count - okCount,
                    items
                };

                logger.LogInformation("Route matrix ok: {Ok}/{Total}", okCount, items.Count);

                return ResponseHelper.CreateSuccessResponse(result);
            }

            logger.LogWarning("No route matrix data returned");
            return ResponseHelper.CreateErrorResponse("No route matrix data returned");
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Maps API error during route matrix calculation: {Message}", ex.Message);
            return ResponseHelper.CreateErrorResponse($"API Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during route matrix calculation");
            return ResponseHelper.CreateErrorResponse("An unexpected error occurred");
        }
    }

    /// <summary>
    /// Calculate the area reachable within a given time or distance from a starting point
    /// </summary>
    [Function(nameof(GetRouteRange))]
    public async Task<string> GetRouteRange(
        [McpToolTrigger(
            "routing_range",
            "Reachable area polygon by time or distance from a point."
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
            "timeBudgetInSeconds",
            "number",
            "Integer seconds; XOR with distanceBudgetInMeters"
        )] int? timeBudgetInSeconds = null,
        [McpToolProperty(
            "distanceBudgetInMeters",
            "number",
            "Integer meters; XOR with timeBudgetInSeconds"
        )] int? distanceBudgetInMeters = null,
        [McpToolProperty(
            "travelMode",
            "string",
            "car|truck|taxi|bus|van|motorcycle|bicycle|pedestrian"
        )] string travelMode = "car",
        [McpToolProperty(
            "routeType",
            "string",
            "fastest|shortest"
        )] string routeType = "fastest"
    )
    {
        try
        {
            if (!ToolsHelper.TryCreateGeoPosition(latitude, longitude, out var centerPoint, out var coordError))
                return ResponseHelper.CreateErrorResponse(coordError!);

            if (!timeBudgetInSeconds.HasValue && !distanceBudgetInMeters.HasValue)
            {
                return ResponseHelper.CreateErrorResponse("Either timeBudgetInSeconds or distanceBudgetInMeters must be specified");
            }

            if (timeBudgetInSeconds.HasValue && distanceBudgetInMeters.HasValue)
            {
                return ResponseHelper.CreateErrorResponse("Specify either timeBudgetInSeconds or distanceBudgetInMeters, not both");
            }

            logger.LogInformation("Calculating route range from coordinates: {Latitude}, {Longitude}", latitude, longitude);

            // Validate travel mode options
            var tmParsed = ToolsHelper.ParseTravelMode(travelMode);
            if (!tmParsed.IsValid) return ResponseHelper.CreateErrorResponse(tmParsed.Error!);
            var parsedTravelMode = tmParsed.Value;

            // Validate route type options
            var rtParsed = ToolsHelper.ParseRouteType(routeType);
            if (!rtParsed.IsValid) return ResponseHelper.CreateErrorResponse(rtParsed.Error!);
            var parsedRouteType = rtParsed.Value;

            var options = new RouteRangeOptions(centerPoint)
            {
                TravelMode = parsedTravelMode,
                RouteType = parsedRouteType,
                UseTrafficData = true
            };

            if (timeBudgetInSeconds.HasValue)
            {
                options.TimeBudget = TimeSpan.FromSeconds(timeBudgetInSeconds.Value);
            }
            else if (distanceBudgetInMeters.HasValue)
            {
                options.DistanceBudgetInMeters = distanceBudgetInMeters.Value;
            }

            var response = await _routingClient.GetRouteRangeAsync(options);

            if (response.Value?.ReachableRange != null)
            {
                var rr = response.Value.ReachableRange;
                var center = new[] { rr.Center.Latitude, rr.Center.Longitude };
                var budget = timeBudgetInSeconds.HasValue
                    ? (object)new { t_s = timeBudgetInSeconds.Value }
                    : new { d_m = distanceBudgetInMeters!.Value };
                var boundary = rr.Boundary?.Select(p => new[] { p.Latitude, p.Longitude }).ToList();
                var result = new { center, budget, mode = travelMode, type = routeType, boundary, n = boundary?.Count ?? 0 };

                logger.LogInformation("Route range ok: {Points} points", result.n);
                return ResponseHelper.CreateSuccessResponse(result);
            }

            logger.LogWarning("No reachable range data returned for coordinates: {Latitude}, {Longitude}", latitude, longitude);
            return ResponseHelper.CreateErrorResponse("No reachable range data returned");
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Maps API error during route range calculation: {Message}", ex.Message);
            return ResponseHelper.CreateErrorResponse($"API Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during route range calculation");
            return ResponseHelper.CreateErrorResponse("An unexpected error occurred");
        }
    }

    /// <summary>
    /// Analyze route waypoints and identify countries traversed
    /// </summary>
    [Function(nameof(AnalyzeRouteCountries))]
    public async Task<string> AnalyzeRouteCountries(
        [McpToolTrigger(
            "routing_countries",
            "Identify countries along a route defined by waypoints. Useful for cross-border checks."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "coordinates",
            "array",
            "Array of {latitude,longitude}. Min 2 points."
    )] LatLon[] coordinates
    )
    {
        try
        {
            var coordsValidation = ValidationHelper.ValidateCoordinateArray(coordinates, c => (c.Latitude, c.Longitude), 2);
            if (!coordsValidation.IsValid)
                return ResponseHelper.CreateErrorResponse(coordsValidation.ErrorMessage!);

            var routePoints = coordinates.Select(c => new GeoPosition(c.Longitude, c.Latitude)).ToList();

            logger.LogInformation("Analyzing route countries for {Count} waypoints", routePoints.Count);

            // Use reverse geocoding to identify countries at waypoints
            // In a more advanced implementation, we could calculate the actual route and sample points along it
            var countriesFound = new HashSet<string>();
            var waypointDetails = new List<object>();
            
            for (int i = 0; i < routePoints.Count; i++)
            {
                try
                {
                    var point = routePoints[i];
                    
                    // Use Azure Maps Search to reverse geocode the point
                    var searchClient = azureMapsService.SearchClient;
                    var response = await searchClient.GetReverseGeocodingAsync(point);
                    
                    string? countryCode = null;
                    string? countryName = null;
                    Country? countryInfo = null;

                    if (response.Value?.Features != null && response.Value.Features.Any())
                    {
                        var feature = response.Value.Features.First();
                        var address = feature.Properties.Address;

                        countryCode = address.CountryRegion.Iso;
                        countryName = address.CountryRegion.Name;
                        if (!string.IsNullOrWhiteSpace(countryCode))
                        {
                            countriesFound.Add(countryCode);
                            countryInfo = _countryHelper.GetCountryByCode(countryCode);
                        }
                    }
                    else
                    {
                        logger.LogDebug("No country found for waypoint {Index}: {Latitude}, {Longitude}", i, point.Latitude, point.Longitude);
                    }
                    
                    waypointDetails.Add(new
                    {
                        WaypointIndex = i,
                        Coordinates = new { Latitude = point.Latitude, Longitude = point.Longitude },
                        CountryCode = countryCode,
                        CountryName = countryName,
                        CountryInfo = countryInfo,
                        Address = response.Value?.Features?.FirstOrDefault()?.Properties.Address?.FormattedAddress
                    });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to analyze waypoint {Index}: {Message}", i, ex.Message);
                    waypointDetails.Add(new
                    {
                        WaypointIndex = i,
                        Coordinates = new { Latitude = routePoints[i].Latitude, Longitude = routePoints[i].Longitude },
                        Error = ex.Message
                    });
                }
            }

            // Get detailed information for all unique countries found
            var uniqueCountries = new List<Country>();
            foreach (var countryCode in countriesFound)
            {
                var country = _countryHelper.GetCountryByCode(countryCode);
                if (country != null)
                {
                    uniqueCountries.Add(country);
                }
            }

            var result = new
            {
                RouteSummary = new
                {
                    TotalWaypoints = routePoints.Count,
                    CountriesTraversed = countriesFound.Count,
                    CountryCodes = countriesFound.ToArray()
                },
                WaypointAnalysis = waypointDetails,
                CountriesDetailed = uniqueCountries,
                TravelConsiderations = uniqueCountries.Count > 1 ? new
                {
                    InternationalTravel = true,
                    BorderCrossings = uniqueCountries.Count - 1,
                    RecommendedChecks = new[]
                    {
                        "Verify passport validity (6+ months remaining)",
                        "Check visa requirements for destination countries",
                        "Review customs regulations for items being transported",
                        "Consider international driving permits if applicable",
                        "Check currency exchange rates and payment methods",
                        "Verify international mobile/data roaming plans"
                    }
                } : null
            };

            logger.LogInformation("Completed route country analysis: {Countries} countries detected", countriesFound.Count);
            return ResponseHelper.CreateSuccessResponse(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during route country analysis");
            return ResponseHelper.CreateErrorResponse("An unexpected error occurred");
        }
    }
}