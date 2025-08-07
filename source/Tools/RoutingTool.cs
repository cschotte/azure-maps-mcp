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

namespace Azure.Maps.Mcp.Tools;

/// <summary>
/// Azure Maps Routing Tool providing route directions, route matrix, and route range capabilities
/// </summary>
public class RoutingTool(IAzureMapsService azureMapsService, ILogger<RoutingTool> logger)
{
    private readonly MapsRoutingClient _routingClient = azureMapsService.RoutingClient;
    private readonly CountryHelper _countryHelper = new();

    /// <summary>
    /// Calculate route directions between coordinates
    /// </summary>
    [Function(nameof(GetRouteDirections))]
    public async Task<string> GetRouteDirections(
        [McpToolTrigger(
            "get_route_directions",
            "Calculate detailed driving/walking/cycling directions between two or more geographic coordinates. Returns comprehensive route information including total distance, estimated travel time, turn-by-turn navigation instructions, and route geometry. Supports multiple travel modes (car, bicycle, pedestrian, etc.) and route optimization preferences (fastest vs shortest). Can handle waypoints for multi-stop routes."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "coordinates",
            "string",
            "JSON array of coordinate objects with latitude and longitude properties. Must include at least 2 points (origin and destination). Additional points will be treated as waypoints. Format: '[{\"latitude\": 47.6062, \"longitude\": -122.3321}, {\"latitude\": 47.6205, \"longitude\": -122.3493}]'. First coordinate is origin, last is destination."
        )] string coordinates,
        [McpToolProperty(
            "travelMode",
            "string",
            "Mode of travel: 'car' (default), 'truck', 'taxi', 'bus', 'van', 'motorcycle', 'bicycle', 'pedestrian'"
        )] string travelMode = "car",
        [McpToolProperty(
            "routeType",
            "string",
            "Type of route optimization: 'fastest' (default), 'shortest'"
        )] string routeType = "fastest",
        [McpToolProperty(
            "avoidTolls",
            "string",
            "Whether to avoid toll roads: 'true' or 'false' (default: 'false')"
        )] string avoidTolls = "false",
        [McpToolProperty(
            "avoidHighways",
            "string",
            "Whether to avoid highways: 'true' or 'false' (default: 'false')"
        )] string avoidHighways = "false"
    )
    {
        try
        {
            var coordinateList = JsonSerializer.Deserialize<List<Dictionary<string, double>>>(coordinates);
            
            if (coordinateList == null || coordinateList.Count < 2)
            {
                return JsonSerializer.Serialize(new { error = "At least 2 coordinates (origin and destination) are required" });
            }

            var routePoints = new List<GeoPosition>();
            foreach (var coord in coordinateList)
            {
                if (!coord.ContainsKey("latitude") || !coord.ContainsKey("longitude"))
                {
                    return JsonSerializer.Serialize(new { error = "Each coordinate must have 'latitude' and 'longitude' properties" });
                }

                var lat = coord["latitude"];
                var lon = coord["longitude"];

                if (lat < -90 || lat > 90 || lon < -180 || lon > 180)
                {
                    return JsonSerializer.Serialize(new { error = "Invalid coordinate values. Latitude must be between -90 and 90, longitude between -180 and 180" });
                }

                routePoints.Add(new GeoPosition(lon, lat));
            }

            logger.LogInformation("Calculating route directions for {Count} points", routePoints.Count);

            // Validate travel mode options
            var validTravelModes = new Dictionary<string, TravelMode>(StringComparer.OrdinalIgnoreCase)
            {
                { "car", TravelMode.Car },
                { "truck", TravelMode.Truck },
                { "taxi", TravelMode.Taxi },
                { "bus", TravelMode.Bus },
                { "van", TravelMode.Van },
                { "motorcycle", TravelMode.Motorcycle },
                { "bicycle", TravelMode.Bicycle },
                { "pedestrian", TravelMode.Pedestrian }
            };

            if (!validTravelModes.TryGetValue(travelMode, out var parsedTravelMode))
            {
                var validOptions = string.Join(", ", validTravelModes.Keys);
                return JsonSerializer.Serialize(new { error = $"Invalid travel mode '{travelMode}'. Valid options: {validOptions}" });
            }

            // Validate route type options
            var validRouteTypes = new Dictionary<string, RouteType>(StringComparer.OrdinalIgnoreCase)
            {
                { "fastest", RouteType.Fastest },
                { "shortest", RouteType.Shortest }
            };

            if (!validRouteTypes.TryGetValue(routeType, out var parsedRouteType))
            {
                var validOptions = string.Join(", ", validRouteTypes.Keys);
                return JsonSerializer.Serialize(new { error = $"Invalid route type '{routeType}'. Valid options: {validOptions}" });
            }

            // Parse boolean parameters
            if (!bool.TryParse(avoidTolls, out var avoidTollsValue))
            {
                return JsonSerializer.Serialize(new { error = $"Invalid avoidTolls value '{avoidTolls}'. Valid options: true, false" });
            }

            if (!bool.TryParse(avoidHighways, out var avoidHighwaysValue))
            {
                return JsonSerializer.Serialize(new { error = $"Invalid avoidHighways value '{avoidHighways}'. Valid options: true, false" });
            }

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
                
                var result = new
                {
                    Summary = new
                    {
                        DistanceInMeters = route.Summary.LengthInMeters,
                        DistanceInKilometers = route.Summary.LengthInMeters.HasValue 
                            ? Math.Round((double)route.Summary.LengthInMeters.Value / 1000.0, 2) 
                            : (double?)null,
                        TravelTimeInSeconds = route.Summary.TravelTimeDuration?.TotalSeconds,
                        TravelTimeFormatted = route.Summary.TravelTimeDuration?.ToString(@"hh\:mm\:ss"),
                        DepartureTime = route.Summary.DepartureTime,
                        ArrivalTime = route.Summary.ArrivalTime
                    },
                    Instructions = route.Guidance?.Instructions?.Select(instruction => new
                    {
                        Text = instruction.Message,
                        DistanceInMeters = instruction.RouteOffsetInMeters,
                        TravelTimeInSeconds = instruction.TravelTimeInSeconds,
                        ManeuverType = instruction.Maneuver?.ToString(),
                        TurnAngleInDegrees = instruction.TurnAngleInDegrees,
                        RoadNumbers = instruction.RoadNumbers,
                        SignpostText = instruction.SignpostText
                    }).ToList(),
                    RouteGeometry = route.Legs?.SelectMany(leg => leg.Points ?? new List<GeoPosition>())
                        .Select(point => new { Latitude = point.Latitude, Longitude = point.Longitude }).ToList(),
                    Legs = route.Legs?.Select((leg, index) => new
                    {
                        LegIndex = index,
                        Summary = new
                        {
                            DistanceInMeters = leg.Summary.LengthInMeters,
                            TravelTimeInSeconds = leg.Summary.TravelTimeInSeconds,
                            TrafficDelayInSeconds = leg.Summary.TrafficDelayInSeconds
                        }
                    }).ToList()
                };

                logger.LogInformation("Successfully calculated route: {Distance}km, {Time}", 
                    result.Summary.DistanceInKilometers, 
                    result.Summary.TravelTimeFormatted);

                return JsonSerializer.Serialize(new { success = true, result }, new JsonSerializerOptions { WriteIndented = true });
            }

            logger.LogWarning("No route found between the specified coordinates");
            return JsonSerializer.Serialize(new { success = false, message = "No route found between the specified coordinates" });
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid JSON format for coordinates");
            return JsonSerializer.Serialize(new { error = "Invalid coordinates format. Expected JSON array of coordinate objects." });
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
            "get_route_matrix",
            "Calculate travel times and distances between multiple origin and destination points in a matrix format. This is essential for optimization scenarios like delivery route planning, finding closest locations, or logistics optimization. Returns a comprehensive matrix showing travel time and distance from each origin to each destination, enabling efficient route planning and location analysis."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "origins",
            "string",
            "JSON array of origin coordinate objects with latitude and longitude properties. Format: '[{\"latitude\": 47.6062, \"longitude\": -122.3321}, {\"latitude\": 47.6205, \"longitude\": -122.3493}]'. Each coordinate represents a starting point for route calculations."
        )] string origins,
        [McpToolProperty(
            "destinations",
            "string",
            "JSON array of destination coordinate objects with latitude and longitude properties. Format: '[{\"latitude\": 47.6062, \"longitude\": -122.3321}, {\"latitude\": 47.6205, \"longitude\": -122.3493}]'. Each coordinate represents an ending point for route calculations."
        )] string destinations,
        [McpToolProperty(
            "travelMode",
            "string",
            "Mode of travel: 'car' (default), 'truck', 'taxi', 'bus', 'van', 'motorcycle', 'bicycle', 'pedestrian'"
        )] string travelMode = "car",
        [McpToolProperty(
            "routeType",
            "string",
            "Type of route optimization: 'fastest' (default), 'shortest'"
        )] string routeType = "fastest"
    )
    {
        try
        {
            var originsList = JsonSerializer.Deserialize<List<Dictionary<string, double>>>(origins);
            var destinationsList = JsonSerializer.Deserialize<List<Dictionary<string, double>>>(destinations);
            
            if (originsList == null || originsList.Count == 0)
            {
                return JsonSerializer.Serialize(new { error = "At least one origin coordinate is required" });
            }

            if (destinationsList == null || destinationsList.Count == 0)
            {
                return JsonSerializer.Serialize(new { error = "At least one destination coordinate is required" });
            }

            // Convert to GeoPosition lists
            var originPoints = new List<GeoPosition>();
            var destinationPoints = new List<GeoPosition>();

            foreach (var coord in originsList)
            {
                if (!coord.ContainsKey("latitude") || !coord.ContainsKey("longitude"))
                {
                    return JsonSerializer.Serialize(new { error = "Each origin coordinate must have 'latitude' and 'longitude' properties" });
                }

                var lat = coord["latitude"];
                var lon = coord["longitude"];

                if (lat < -90 || lat > 90 || lon < -180 || lon > 180)
                {
                    return JsonSerializer.Serialize(new { error = "Invalid origin coordinate values" });
                }

                originPoints.Add(new GeoPosition(lon, lat));
            }

            foreach (var coord in destinationsList)
            {
                if (!coord.ContainsKey("latitude") || !coord.ContainsKey("longitude"))
                {
                    return JsonSerializer.Serialize(new { error = "Each destination coordinate must have 'latitude' and 'longitude' properties" });
                }

                var lat = coord["latitude"];
                var lon = coord["longitude"];

                if (lat < -90 || lat > 90 || lon < -180 || lon > 180)
                {
                    return JsonSerializer.Serialize(new { error = "Invalid destination coordinate values" });
                }

                destinationPoints.Add(new GeoPosition(lon, lat));
            }

            logger.LogInformation("Calculating route matrix for {OriginCount} origins and {DestinationCount} destinations", 
                originPoints.Count, destinationPoints.Count);

            // Validate travel mode options
            var validTravelModes = new Dictionary<string, TravelMode>(StringComparer.OrdinalIgnoreCase)
            {
                { "car", TravelMode.Car },
                { "truck", TravelMode.Truck },
                { "taxi", TravelMode.Taxi },
                { "bus", TravelMode.Bus },
                { "van", TravelMode.Van },
                { "motorcycle", TravelMode.Motorcycle },
                { "bicycle", TravelMode.Bicycle },
                { "pedestrian", TravelMode.Pedestrian }
            };

            if (!validTravelModes.TryGetValue(travelMode, out var parsedTravelMode))
            {
                var validOptions = string.Join(", ", validTravelModes.Keys);
                return JsonSerializer.Serialize(new { error = $"Invalid travel mode '{travelMode}'. Valid options: {validOptions}" });
            }

            // Validate route type options
            var validRouteTypes = new Dictionary<string, RouteType>(StringComparer.OrdinalIgnoreCase)
            {
                { "fastest", RouteType.Fastest },
                { "shortest", RouteType.Shortest }
            };

            if (!validRouteTypes.TryGetValue(routeType, out var parsedRouteType))
            {
                var validOptions = string.Join(", ", validRouteTypes.Keys);
                return JsonSerializer.Serialize(new { error = $"Invalid route type '{routeType}'. Valid options: {validOptions}" });
            }

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
                var results = new List<object>();

                for (int i = 0; i < matrix.Count; i++)
                {
                    var row = matrix[i];
                    for (int j = 0; j < row.Count; j++)
                    {
                        var cell = row[j];
                        
                        results.Add(new
                        {
                            OriginIndex = i,
                            DestinationIndex = j,
                            OriginCoordinate = new 
                            { 
                                Latitude = originPoints[i].Latitude, 
                                Longitude = originPoints[i].Longitude 
                            },
                            DestinationCoordinate = new 
                            { 
                                Latitude = destinationPoints[j].Latitude, 
                                Longitude = destinationPoints[j].Longitude 
                            },
                            Response = cell.Summary != null ? new
                            {
                                DistanceInMeters = cell.Summary.LengthInMeters,
                                DistanceInKilometers = cell.Summary.LengthInMeters.HasValue 
                                    ? Math.Round((double)cell.Summary.LengthInMeters.Value / 1000.0, 2) 
                                    : (double?)null,
                                TravelTimeInSeconds = cell.Summary.TravelTimeInSeconds,
                                TravelTimeFormatted = cell.Summary.TravelTimeInSeconds.HasValue
                                    ? TimeSpan.FromSeconds(cell.Summary.TravelTimeInSeconds.Value).ToString(@"hh\:mm\:ss")
                                    : null,
                                TrafficDelayInSeconds = cell.Summary.TrafficDelayInSeconds,
                                DepartureTime = cell.Summary.DepartureTime,
                                ArrivalTime = cell.Summary.ArrivalTime
                            } : null,
                            Error = cell.Summary == null ? "Route not found" : null
                        });
                    }
                }

                var result = new
                {
                    Summary = new
                    {
                        OriginCount = originPoints.Count,
                        DestinationCount = destinationPoints.Count,
                        TotalCombinations = results.Count,
                        SuccessfulRoutes = results.Count(r => ((dynamic)r).Response != null),
                        FailedRoutes = results.Count(r => ((dynamic)r).Response == null)
                    },
                    Matrix = results
                };

                logger.LogInformation("Successfully calculated route matrix: {Successful}/{Total} routes", 
                    result.Summary.SuccessfulRoutes, result.Summary.TotalCombinations);

                return JsonSerializer.Serialize(new { success = true, result }, new JsonSerializerOptions { WriteIndented = true });
            }

            logger.LogWarning("No route matrix data returned");
            return JsonSerializer.Serialize(new { success = false, message = "No route matrix data returned" });
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid JSON format for coordinates");
            return JsonSerializer.Serialize(new { error = "Invalid coordinates format. Expected JSON array of coordinate objects." });
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
            "get_route_range",
            "Calculate the geographic area reachable within a specified time limit or distance from a starting point. This creates an 'isochrone' or 'isodistance' polygon showing all locations accessible within the given constraints. Useful for service area analysis, delivery zone planning, emergency response coverage, and accessibility studies. Returns polygon coordinates that define the reachable boundary."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "latitude",
            "string",
            "Starting point latitude coordinate as a decimal number (e.g., '47.6062'). Must be between -90 and 90 degrees."
        )] double latitude,
        [McpToolProperty(
            "longitude",
            "string",
            "Starting point longitude coordinate as a decimal number (e.g., '-122.3321'). Must be between -180 and 180 degrees."
        )] double longitude,
        [McpToolProperty(
            "timeBudgetInSeconds",
            "string",
            "Time budget in seconds for reachability calculation (e.g., '1800' for 30 minutes). Use either this OR distanceBudgetInMeters, not both. This defines how far you can travel within the given time."
        )] int? timeBudgetInSeconds = null,
        [McpToolProperty(
            "distanceBudgetInMeters",
            "string",
            "Distance budget in meters for reachability calculation (e.g., '5000' for 5km). Use either this OR timeBudgetInSeconds, not both. This defines the maximum distance you can travel."
        )] int? distanceBudgetInMeters = null,
        [McpToolProperty(
            "travelMode",
            "string",
            "Mode of travel: 'car' (default), 'truck', 'taxi', 'bus', 'van', 'motorcycle', 'bicycle', 'pedestrian'"
        )] string travelMode = "car",
        [McpToolProperty(
            "routeType",
            "string",
            "Type of route optimization: 'fastest' (default), 'shortest'"
        )] string routeType = "fastest"
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

            if (!timeBudgetInSeconds.HasValue && !distanceBudgetInMeters.HasValue)
            {
                return JsonSerializer.Serialize(new { error = "Either timeBudgetInSeconds or distanceBudgetInMeters must be specified" });
            }

            if (timeBudgetInSeconds.HasValue && distanceBudgetInMeters.HasValue)
            {
                return JsonSerializer.Serialize(new { error = "Specify either timeBudgetInSeconds or distanceBudgetInMeters, not both" });
            }

            var centerPoint = new GeoPosition(longitude, latitude);

            logger.LogInformation("Calculating route range from coordinates: {Latitude}, {Longitude}", latitude, longitude);

            // Validate travel mode options
            var validTravelModes = new Dictionary<string, TravelMode>(StringComparer.OrdinalIgnoreCase)
            {
                { "car", TravelMode.Car },
                { "truck", TravelMode.Truck },
                { "taxi", TravelMode.Taxi },
                { "bus", TravelMode.Bus },
                { "van", TravelMode.Van },
                { "motorcycle", TravelMode.Motorcycle },
                { "bicycle", TravelMode.Bicycle },
                { "pedestrian", TravelMode.Pedestrian }
            };

            if (!validTravelModes.TryGetValue(travelMode, out var parsedTravelMode))
            {
                var validOptions = string.Join(", ", validTravelModes.Keys);
                return JsonSerializer.Serialize(new { error = $"Invalid travel mode '{travelMode}'. Valid options: {validOptions}" });
            }

            // Validate route type options
            var validRouteTypes = new Dictionary<string, RouteType>(StringComparer.OrdinalIgnoreCase)
            {
                { "fastest", RouteType.Fastest },
                { "shortest", RouteType.Shortest }
            };

            if (!validRouteTypes.TryGetValue(routeType, out var parsedRouteType))
            {
                var validOptions = string.Join(", ", validRouteTypes.Keys);
                return JsonSerializer.Serialize(new { error = $"Invalid route type '{routeType}'. Valid options: {validOptions}" });
            }

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
                var reachableRange = response.Value.ReachableRange;
                
                var result = new
                {
                    Center = new
                    {
                        Latitude = reachableRange.Center.Latitude,
                        Longitude = reachableRange.Center.Longitude
                    },
                    Budget = timeBudgetInSeconds.HasValue 
                        ? (object)new { TimeBudgetInSeconds = timeBudgetInSeconds.Value, TimeBudgetFormatted = TimeSpan.FromSeconds(timeBudgetInSeconds.Value).ToString(@"hh\:mm\:ss") }
                        : new { DistanceBudgetInMeters = distanceBudgetInMeters!.Value, DistanceBudgetInKilometers = Math.Round(distanceBudgetInMeters.Value / 1000.0, 2) },
                    TravelMode = travelMode,
                    RouteType = routeType,
                    Boundary = reachableRange.Boundary?.Select(point => new
                    {
                        Latitude = point.Latitude,
                        Longitude = point.Longitude
                    }).ToList(),
                    BoundaryPointCount = reachableRange.Boundary?.Count ?? 0
                };

                logger.LogInformation("Successfully calculated route range with {PointCount} boundary points", 
                    result.BoundaryPointCount);

                return JsonSerializer.Serialize(new { success = true, result }, new JsonSerializerOptions { WriteIndented = true });
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
            "analyze_route_countries",
            "Analyze a route and identify all countries that the route passes through. This is valuable for international travel planning, customs preparation, visa requirements analysis, and understanding cross-border logistics. Returns detailed country information for each country along the route path."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "coordinates",
            "string",
            "JSON array of coordinate objects representing the route path. Must include at least 2 points (origin and destination). Format: '[{\"latitude\": 47.6062, \"longitude\": -122.3321}, {\"latitude\": 48.8566, \"longitude\": 2.3522}]'. More points provide better country detection accuracy."
        )] string coordinates
    )
    {
        try
        {
            var coordinateList = JsonSerializer.Deserialize<List<Dictionary<string, double>>>(coordinates);
            
            if (coordinateList == null || coordinateList.Count < 2)
            {
                return JsonSerializer.Serialize(new { error = "At least 2 coordinates (origin and destination) are required" });
            }

            var routePoints = new List<GeoPosition>();
            foreach (var coord in coordinateList)
            {
                if (!coord.ContainsKey("latitude") || !coord.ContainsKey("longitude"))
                {
                    return JsonSerializer.Serialize(new { error = "Each coordinate must have 'latitude' and 'longitude' properties" });
                }

                var lat = coord["latitude"];
                var lon = coord["longitude"];

                if (lat < -90 || lat > 90 || lon < -180 || lon > 180)
                {
                    return JsonSerializer.Serialize(new { error = "Invalid coordinate values. Latitude must be between -90 and 90, longitude between -180 and 180" });
                }

                routePoints.Add(new GeoPosition(lon, lat));
            }

            logger.LogInformation("Analyzing route countries for {Count} waypoints", routePoints.Count);

            // For this analysis, we'll use reverse geocoding to identify countries at key points
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
                        var countryRegion = feature.Properties.Address?.CountryRegion?.ToString();
                        
                        if (!string.IsNullOrEmpty(countryRegion) && countryRegion.Length == 2)
                        {
                            countryCode = countryRegion;
                            countryInfo = _countryHelper.GetCountryByCode(countryCode);
                            if (countryInfo != null)
                            {
                                countryName = countryInfo.CountryName;
                                countriesFound.Add(countryCode);
                            }
                        }
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
            return JsonSerializer.Serialize(new { success = true, result }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid JSON format for coordinates");
            return JsonSerializer.Serialize(new { error = "Invalid coordinates format. Expected JSON array of coordinate objects." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during route country analysis");
            return JsonSerializer.Serialize(new { error = "An unexpected error occurred" });
        }
    }
}