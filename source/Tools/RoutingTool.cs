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

namespace Azure.Maps.Mcp.Tools;

/// <summary>
/// Represents a coordinate for routing operations
/// </summary>
public class CoordinateInfo
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

/// <summary>
/// Azure Maps Routing Tool providing route directions, route matrix, and route range capabilities
/// </summary>
public class RoutingTool(IAzureMapsService azureMapsService, ILogger<RoutingTool> logger)
{
    private readonly MapsRoutingClient _routingClient = azureMapsService.RoutingClient;
    private readonly CountryHelper _countryHelper = new();
    
    private static bool TryValidateCoordinate(CoordinateInfo coord, out GeoPosition position)
    {
        var ok = ToolsHelper.TryCreateGeoPosition(coord.Latitude, coord.Longitude, out position, out _);
        return ok;
    }

    /// <summary>
    /// Calculate route directions between coordinates
    /// </summary>
    [Function(nameof(GetRouteDirections))]
    public async Task<string> GetRouteDirections(
        [McpToolTrigger(
            "routing_directions",
            "🛣️ ROUTE PLANNER: Calculate optimized route directions between multiple points with real-time traffic integration. Perfect for trip planning, logistics optimization, and navigation applications. Supports multi-modal transportation with detailed turn-by-turn instructions, traffic-aware timing, and route geometry. Returns comprehensive route information including distance, time estimates, traffic delays, step-by-step directions, and route coordinates for mapping. BEST FOR: travel planning, delivery optimization, multi-stop tours, accessibility routing, logistics planning. FEATURES: Real-time traffic integration, route optimization, waypoint reordering, toll/highway avoidance options."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "coordinates",
            "array",
            "📍 ROUTE POINTS: Array of coordinate objects for route calculation. MINIMUM: 2 points (origin, destination). FORMAT: [{latitude: number, longitude: number}, ...]. MULTIPLE POINTS: Add intermediate waypoints for complex routes with automatic optimization. EXAMPLE: [{latitude: 47.6062, longitude: -122.3321}, {latitude: 47.6205, longitude: -122.3493}]. TIP: More waypoints provide better route optimization and enhanced country detection for international travel."
        )] CoordinateInfo[] coordinates,
        [McpToolProperty(
            "travelMode",
            "string",
            "🚗 TRANSPORTATION MODE: Vehicle/travel type for route optimization. OPTIONS: 'car' (default - fastest routes, traffic-optimized), 'truck' (respects weight/height restrictions, commercial routes), 'taxi' (passenger pickup optimized, urban routing), 'bus' (public transit compatible routes), 'van' (delivery optimized, moderate restrictions), 'motorcycle' (allows lane filtering, bike-accessible), 'bicycle' (bike lanes, cycling paths, elevation-aware), 'pedestrian' (walking paths, sidewalks, pedestrian areas). Each mode optimizes for specific vehicle constraints and road access permissions."
        )] string travelMode = "car",
        [McpToolProperty(
            "routeType",
            "string",
            "⚡ ROUTE OPTIMIZATION: Route calculation priority strategy. 'fastest' (default - time-optimized with real-time traffic data, recommended for most use cases), 'shortest' (distance-optimized, may take longer but uses less fuel, good for cost optimization). Choose 'fastest' for time-sensitive travel and deliveries, 'shortest' for fuel efficiency or simple distance-based routing."
        )] string routeType = "fastest",
        [McpToolProperty(
            "avoidTolls",
            "string",
            "💰 TOLL AVOIDANCE: Avoid toll roads and bridges (boolean as string). 'true' = avoid all toll roads (may increase travel time but reduce costs), 'false' (default) = allow toll roads for optimal routing. Useful for cost-conscious routing or when traveling without toll payment methods."
        )] string avoidTolls = "false",
        [McpToolProperty(
            "avoidHighways",
            "string",
            "🛣️ HIGHWAY AVOIDANCE: Avoid major highways and freeways (boolean as string). 'true' = use local roads and smaller highways (scenic routes, slower but more local access), 'false' (default) = allow all highway types for optimal speed. Useful for scenic routing, local business access, or when vehicle restrictions apply to major highways."
        )] string avoidHighways = "false"
    )
    {
        try
        {
            var coordsValidation = ValidationHelper.ValidateCoordinateArray(coordinates, c => (c.Latitude, c.Longitude), 2);
            if (!coordsValidation.IsValid)
                return JsonSerializer.Serialize(new { error = coordsValidation.ErrorMessage });

            var routePoints = coordinates
                .Select(c => new GeoPosition(c.Longitude, c.Latitude))
                .ToList();

            logger.LogInformation("Calculating route directions for {Count} points", routePoints.Count);

            // Validate travel mode options
            var tmParsed = ToolsHelper.ParseTravelMode(travelMode);
            if (!tmParsed.IsValid) return JsonSerializer.Serialize(new { error = tmParsed.Error });
            var parsedTravelMode = tmParsed.Value;

            // Validate route type options
            var rtParsed = ToolsHelper.ParseRouteType(routeType);
            if (!rtParsed.IsValid) return JsonSerializer.Serialize(new { error = rtParsed.Error });
            var parsedRouteType = rtParsed.Value;

            // Parse boolean parameters using shared validator
            var avoidTollsParse = ValidationHelper.ValidateBooleanString(avoidTolls, nameof(avoidTolls));
            if (!avoidTollsParse.IsValid)
                return JsonSerializer.Serialize(new { error = avoidTollsParse.ErrorMessage });

            var avoidHighwaysParse = ValidationHelper.ValidateBooleanString(avoidHighways, nameof(avoidHighways));
            if (!avoidHighwaysParse.IsValid)
                return JsonSerializer.Serialize(new { error = avoidHighwaysParse.ErrorMessage });

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

                var aiOptimizedResult = new
                {
                    success = true,
                    tool = "routing_directions",
                    timestamp = DateTime.UtcNow.ToString("O"),
                    route = new
                    {
                        summary = new
                        {
                            distanceMeters = dist_m,
                            distanceKilometers = dist_m.HasValue ? Math.Round(dist_m.Value / 1000.0, 2) : (double?)null,
                            travelTimeSeconds = time_s,
                            travelTimeDuration = time_s.HasValue ? TimeSpan.FromSeconds(time_s.Value).ToString(@"hh\:mm\:ss") : null,
                            trafficDelaySeconds = route.Summary.TravelTimeDuration?.TotalSeconds
                        },
                        geometry = route.Legs?.SelectMany(leg => leg.Points ?? new List<GeoPosition>())
                            .Select(p => new { latitude = p.Latitude, longitude = p.Longitude }).ToList(),
                        legs = legs,
                        instructions = instructions,
                        quality = new
                        {
                            routeOptimization = parsedRouteType.ToString(),
                            trafficData = "Real-time traffic included",
                            accuracy = "High-precision routing"
                        }
                    },
                    aiContext = new
                    {
                        toolCategory = "NAVIGATION",
                        nextSuggestedActions = new[]
                        {
                            "Use geometry coordinates for map visualization",
                            "Process instructions for turn-by-turn navigation",
                            "Monitor traffic delays for real-time updates",
                            "Consider alternative routes if delays are significant"
                        },
                        usageHints = new[]
                        {
                            $"Route optimized for {parsedTravelMode.ToString().ToLower()} travel",
                            $"Estimated travel time includes current traffic conditions",
                            "Coordinates provided in WGS84 decimal degrees format"
                        }
                    }
                };

                logger.LogInformation("Route ok: {Km}km, {Time}",
                    dist_m.HasValue ? Math.Round(dist_m.Value / 1000.0, 2) : null,
                    time_s.HasValue ? TimeSpan.FromSeconds(time_s.Value).ToString(@"hh\:mm\:ss") : null);

                return JsonSerializer.Serialize(aiOptimizedResult, new JsonSerializerOptions { WriteIndented = false });
            }

            // No route found
            var noRouteResponse = new
            {
                success = false,
                tool = "routing_directions",
                timestamp = DateTime.UtcNow.ToString("O"),
                error = new
                {
                    type = "NO_ROUTE",
                    message = "No route found between the specified coordinates",
                    coordinates = coordinates.Select(c => new { c.Latitude, c.Longitude }).ToArray(),
                    recovery = new
                    {
                        immediateActions = new[]
                        {
                            "Verify all coordinates are accessible by the selected travel mode",
                            "Check if coordinates are in restricted or inaccessible areas",
                            "Try alternative travel modes (e.g., pedestrian if car routing fails)",
                            "Ensure coordinates are not separated by impassable barriers"
                        },
                        commonCauses = new[]
                        {
                            "Coordinates in different road networks (e.g., islands)",
                            "Restricted areas or private roads for selected travel mode",
                            "Coordinates over water without ferry connections",
                            "Travel mode restrictions (e.g., truck routing on car-only roads)"
                        },
                        alternatives = new[]
                        {
                            "Try different travel mode (pedestrian, bicycle, car)",
                            "Use route matrix to check connectivity between points",
                            "Validate coordinates with reverse geocoding first"
                        }
                    }
                }
            };

            logger.LogWarning("No route found between the specified coordinates");
            return JsonSerializer.Serialize(noRouteResponse, new JsonSerializerOptions { WriteIndented = false });
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Maps API error during route calculation: {Message}", ex.Message);
            
            var errorResponse = new
            {
                success = false,
                tool = "routing_directions",
                timestamp = DateTime.UtcNow.ToString("O"),
                error = new
                {
                    type = "API_ERROR",
                    message = $"Azure Maps routing service error: {ex.Message}",
                    coordinates = coordinates.Select(c => new { c.Latitude, c.Longitude }).ToArray(),
                    recovery = new
                    {
                        immediateActions = new[] { "Retry the request", "Check coordinate validity", "Verify service status" },
                        commonCauses = new[] { "Temporary service issue", "Invalid coordinates", "Rate limiting" },
                        examples = "Wait a moment and retry, or check Azure Maps service health"
                    }
                }
            };
            
            return JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions { WriteIndented = false });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during route calculation");
            return JsonSerializer.Serialize(new { error = "An unexpected error occurred" });
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
        )] CoordinateInfo[] origins,
        [McpToolProperty(
            "destinations",
            "array",
            "Array of {latitude,longitude}."
        )] CoordinateInfo[] destinations,
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
                return JsonSerializer.Serialize(new { error = originValidation.ErrorMessage });

            var destValidation = ValidationHelper.ValidateCoordinateArray(destinations, c => (c.Latitude, c.Longitude), 1);
            if (!destValidation.IsValid)
                return JsonSerializer.Serialize(new { error = destValidation.ErrorMessage });

            // Convert to GeoPosition lists
            var originPoints = origins.Select(c => new GeoPosition(c.Longitude, c.Latitude)).ToList();
            var destinationPoints = destinations.Select(c => new GeoPosition(c.Longitude, c.Latitude)).ToList();

            logger.LogInformation("Calculating route matrix for {OriginCount} origins and {DestinationCount} destinations", 
                originPoints.Count, destinationPoints.Count);

            // Validate travel mode options
            var tmParsed = ToolsHelper.ParseTravelMode(travelMode);
            if (!tmParsed.IsValid) return JsonSerializer.Serialize(new { error = tmParsed.Error });
            var parsedTravelMode = tmParsed.Value;

            // Validate route type options
            var rtParsed = ToolsHelper.ParseRouteType(routeType);
            if (!rtParsed.IsValid) return JsonSerializer.Serialize(new { error = rtParsed.Error });
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

                return JsonSerializer.Serialize(new { ok = true, result }, new JsonSerializerOptions { WriteIndented = false });
            }

            logger.LogWarning("No route matrix data returned");
            return JsonSerializer.Serialize(new { success = false, message = "No route matrix data returned" });
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Maps API error during route matrix calculation: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = $"API Error: {ex.Message}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during route matrix calculation");
            return JsonSerializer.Serialize(new { error = "An unexpected error occurred" });
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
            "string",
            "number: -90..90"
        )] double latitude,
        [McpToolProperty(
            "longitude",
            "string",
            "number: -180..180"
        )] double longitude,
        [McpToolProperty(
            "timeBudgetInSeconds",
            "string",
            "integer seconds; use XOR with distanceBudgetInMeters"
        )] int? timeBudgetInSeconds = null,
        [McpToolProperty(
            "distanceBudgetInMeters",
            "string",
            "integer meters; use XOR with timeBudgetInSeconds"
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
                return JsonSerializer.Serialize(new { error = coordError });

            if (!timeBudgetInSeconds.HasValue && !distanceBudgetInMeters.HasValue)
            {
                return JsonSerializer.Serialize(new { error = "Either timeBudgetInSeconds or distanceBudgetInMeters must be specified" });
            }

            if (timeBudgetInSeconds.HasValue && distanceBudgetInMeters.HasValue)
            {
                return JsonSerializer.Serialize(new { error = "Specify either timeBudgetInSeconds or distanceBudgetInMeters, not both" });
            }

            logger.LogInformation("Calculating route range from coordinates: {Latitude}, {Longitude}", latitude, longitude);

            // Validate travel mode options
            var tmParsed = ToolsHelper.ParseTravelMode(travelMode);
            if (!tmParsed.IsValid) return JsonSerializer.Serialize(new { error = tmParsed.Error });
            var parsedTravelMode = tmParsed.Value;

            // Validate route type options
            var rtParsed = ToolsHelper.ParseRouteType(routeType);
            if (!rtParsed.IsValid) return JsonSerializer.Serialize(new { error = rtParsed.Error });
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
                return JsonSerializer.Serialize(new { ok = true, result }, new JsonSerializerOptions { WriteIndented = false });
            }

            logger.LogWarning("No reachable range data returned for coordinates: {Latitude}, {Longitude}", latitude, longitude);
            return JsonSerializer.Serialize(new { success = false, message = "No reachable range data returned" });
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Maps API error during route range calculation: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = $"API Error: {ex.Message}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during route range calculation");
            return JsonSerializer.Serialize(new { error = "An unexpected error occurred" });
        }
    }

    /// <summary>
    /// Analyze route waypoints and identify countries traversed
    /// </summary>
    [Function(nameof(AnalyzeRouteCountries))]
    public async Task<string> AnalyzeRouteCountries(
        [McpToolTrigger(
            "routing_countries",
            "Analyze a route and identify all countries that the route passes through. This is valuable for international travel planning, customs preparation, visa requirements analysis, and understanding cross-border logistics. Returns detailed country information for each country along the route path."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "coordinates",
            "array",
            "Array of coordinate objects representing the route path. Must include at least 2 points (origin and destination). More points provide better country detection accuracy. Example: [{'latitude': 49.2827, 'longitude': -123.1207}, {'latitude': 47.6062, 'longitude': -122.3321}]"
        )] CoordinateInfo[] coordinates
    )
    {
        try
        {
            var coordsValidation = ValidationHelper.ValidateCoordinateArray(coordinates, c => (c.Latitude, c.Longitude), 2);
            if (!coordsValidation.IsValid)
                return JsonSerializer.Serialize(new { error = coordsValidation.ErrorMessage });

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
            return JsonSerializer.Serialize(new { success = true, result }, new JsonSerializerOptions { WriteIndented = false });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during route country analysis");
            return JsonSerializer.Serialize(new { error = "An unexpected error occurred" });
        }
    }
}