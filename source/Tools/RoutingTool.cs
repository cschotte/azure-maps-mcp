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

    private static bool TryParseBool(string value, string name, out bool result, out string? error)
    {
        if (bool.TryParse(value, out result)) { error = null; return true; }
        error = $"Invalid {name} value '{value}'. Valid options: true, false"; return false;
    }
    
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
            "Directions between 2+ points with travelMode/routeType."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "coordinates",
            "array",
            "[{latitude,longitude}, ...] min 2 (origin,destination). Waypoints allowed."
        )] CoordinateInfo[] coordinates,
        [McpToolProperty(
            "travelMode",
            "string",
            "car|truck|taxi|bus|van|motorcycle|bicycle|pedestrian"
        )] string travelMode = "car",
        [McpToolProperty(
            "routeType",
            "string",
            "fastest|shortest"
        )] string routeType = "fastest",
        [McpToolProperty(
            "avoidTolls",
            "string",
            "boolean string: true|false"
        )] string avoidTolls = "false",
        [McpToolProperty(
            "avoidHighways",
            "string",
            "boolean string: true|false"
        )] string avoidHighways = "false"
    )
    {
        try
        {
            if (coordinates == null || coordinates.Length < 2)
            {
                return JsonSerializer.Serialize(new { error = "At least 2 coordinates (origin and destination) are required" });
            }

            var routePoints = new List<GeoPosition>(coordinates.Length);
            foreach (var coord in coordinates)
            {
                if (!TryValidateCoordinate(coord, out var pos))
                    return JsonSerializer.Serialize(new { error = "Invalid coordinate values. Latitude must be between -90 and 90, longitude between -180 and 180" });
                routePoints.Add(pos);
            }

            logger.LogInformation("Calculating route directions for {Count} points", routePoints.Count);

            // Validate travel mode options
            var tmParsed = ToolsHelper.ParseTravelMode(travelMode);
            if (!tmParsed.IsValid) return JsonSerializer.Serialize(new { error = tmParsed.Error });
            var parsedTravelMode = tmParsed.Value;

            // Validate route type options
            var rtParsed = ToolsHelper.ParseRouteType(routeType);
            if (!rtParsed.IsValid) return JsonSerializer.Serialize(new { error = rtParsed.Error });
            var parsedRouteType = rtParsed.Value;

            // Parse boolean parameters
            if (!TryParseBool(avoidTolls, nameof(avoidTolls), out var avoidTollsValue, out var atError))
                return JsonSerializer.Serialize(new { error = atError });
            if (!TryParseBool(avoidHighways, nameof(avoidHighways), out var avoidHighwaysValue, out var ahError))
                return JsonSerializer.Serialize(new { error = ahError });

            var options = new RouteDirectionOptions()
            {
                TravelMode = parsedTravelMode,
                RouteType = parsedRouteType,
                UseTrafficData = true,
                ComputeBestWaypointOrder = routePoints.Count > 2,
                InstructionsType = RouteInstructionsType.Text
            };

            // Configure avoidance options
            if (avoidTollsValue) options.Avoid.Add(RouteAvoidType.TollRoads);
            if (avoidHighwaysValue) options.Avoid.Add(RouteAvoidType.Motorways);

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

                var instr = route.Guidance?.Instructions?.Select(i => new
                {
                    msg = i.Message,
                    off_m = i.RouteOffsetInMeters,
                    t_s = i.TravelTimeInSeconds,
                    man = i.Maneuver?.ToString(),
                    turn_deg = i.TurnAngleInDegrees
                }).ToList();

                var result = new { dist_m, time_s, geom, legs, instr };

                logger.LogInformation("Route ok: {Km}km, {Time}",
                    dist_m.HasValue ? Math.Round(dist_m.Value / 1000.0, 2) : null,
                    time_s.HasValue ? TimeSpan.FromSeconds(time_s.Value).ToString(@"hh\:mm\:ss") : null);

                return JsonSerializer.Serialize(new { ok = true, result }, new JsonSerializerOptions { WriteIndented = false });
            }

            logger.LogWarning("No route found between the specified coordinates");
            return JsonSerializer.Serialize(new { success = false, message = "No route found between the specified coordinates" });
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Maps API error during route calculation: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = $"API Error: {ex.Message}" });
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
            if (origins == null || origins.Length == 0)
            {
                return JsonSerializer.Serialize(new { error = "At least one origin coordinate is required" });
            }

            if (destinations == null || destinations.Length == 0)
            {
                return JsonSerializer.Serialize(new { error = "At least one destination coordinate is required" });
            }

            // Convert to GeoPosition lists
            var originPoints = new List<GeoPosition>(origins.Length);
            foreach (var coord in origins)
            {
                if (!TryValidateCoordinate(coord, out var pos))
                    return JsonSerializer.Serialize(new { error = "Invalid origin coordinate values" });
                originPoints.Add(pos);
            }

            var destinationPoints = new List<GeoPosition>(destinations.Length);
            foreach (var coord in destinations)
            {
                if (!TryValidateCoordinate(coord, out var pos))
                    return JsonSerializer.Serialize(new { error = "Invalid destination coordinate values" });
                destinationPoints.Add(pos);
            }

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
            if (coordinates == null || coordinates.Length < 2)
            {
                return JsonSerializer.Serialize(new { error = "At least 2 coordinates (origin and destination) are required" });
            }

            var routePoints = new List<GeoPosition>();
            foreach (var coord in coordinates)
            {
                var lat = coord.Latitude;
                var lon = coord.Longitude;

                if (lat < -90 || lat > 90 || lon < -180 || lon > 180)
                {
                    return JsonSerializer.Serialize(new { error = "Invalid coordinate values. Latitude must be between -90 and 90, longitude between -180 and 180" });
                }

                routePoints.Add(new GeoPosition(lon, lat));
            }

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