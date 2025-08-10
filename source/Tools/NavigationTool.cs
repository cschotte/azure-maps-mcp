// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using Azure.Core.GeoJson;
using Azure.Maps.Mcp.Services;
using Azure.Maps.Routing;
using Azure.Maps.Mcp.Common;
using Azure.Maps.Mcp.Common.Models;

namespace Azure.Maps.Mcp.Tools;

/// <summary>
/// Simplified Azure Maps Navigation Tool - combining route calculation and analysis
/// Demonstrates reduced complexity approach
/// </summary>
public class NavigationTool : BaseMapsTool
{
    private readonly MapsRoutingClient _routingClient;

    public NavigationTool(IAzureMapsService mapsService, ILogger<NavigationTool> logger)
        : base(mapsService, logger)
    {
        _routingClient = mapsService.RoutingClient;
    }

    /// <summary>
    /// Universal route calculation - handles directions, matrix, and range calculations
    /// </summary>
    [Function(nameof(CalculateRoute))]
    public async Task<string> CalculateRoute(
        [McpToolTrigger(
            "navigation_calculate",
            "Calculate routes, travel matrices, or reachable areas. Supports multiple calculation types."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "coordinates",
            "array",
            "Array of {latitude,longitude} points. Min 1 for range, min 2 for directions/matrix"
        )] LatLon[] coordinates,
        [McpToolProperty(
            "calculationType",
            "string",
            "Type: 'directions' (route between points), 'matrix' (all-to-all distances), 'range' (reachable area). Default: directions"
        )] string calculationType = "directions",
        [McpToolProperty(
            "travelMode",
            "string",
            "Transport mode: car, truck, taxi, bus, van, motorcycle, bicycle, pedestrian. Default: car"
        )] string travelMode = "car",
        [McpToolProperty(
            "routeType",
            "string",
            "Optimization: fastest or shortest. Default: fastest"
        )] string routeType = "fastest",
        [McpToolProperty(
            "timeBudgetMinutes",
            "number",
            "For range calculation: time budget in minutes. Example: 30"
        )] int? timeBudgetMinutes = null,
        [McpToolProperty(
            "distanceBudgetKm",
            "number",
            "For range calculation: distance budget in kilometers. Example: 50"
        )] int? distanceBudgetKm = null,
        [McpToolProperty(
            "avoidTolls",
            "boolean",
            "Avoid toll roads. Default: false"
        )] bool avoidTolls = false,
        [McpToolProperty(
            "avoidHighways",
            "boolean",
            "Avoid highways. Default: false"
        )] bool avoidHighways = false
    )
    {
        return await ExecuteWithErrorHandling(async () =>
        {
            // validation
            var minCoords = calculationType.ToLower() == "range" ? 1 : 2;
            var coordsValidation = ValidationHelper.ValidateCoordinateArray(coordinates, c => (c.Latitude, c.Longitude), minCoords);
            if (!coordsValidation.IsValid) throw new ArgumentException(coordsValidation.ErrorMessage);

            // Validate travel mode
            var tmParsed = ToolsHelper.ParseTravelMode(travelMode);
            if (!tmParsed.IsValid) throw new ArgumentException(tmParsed.Error);

            // Validate route type
            var rtParsed = ToolsHelper.ParseRouteType(routeType);
            if (!rtParsed.IsValid) throw new ArgumentException(rtParsed.Error);

            _logger.LogInformation("Route calculation: {Type} for {Count} points using {Mode} transport",
                calculationType, coordinates.Length, travelMode);

            // Route to appropriate calculation method
            return calculationType.ToLower() switch
            {
        "directions" => await CalculateDirections(coordinates, tmParsed.Value, rtParsed.Value, avoidTolls, avoidHighways),
                "matrix" => await CalculateMatrix(coordinates, tmParsed.Value, rtParsed.Value),
                "range" => await CalculateRange(coordinates[0], tmParsed.Value, rtParsed.Value, timeBudgetMinutes, distanceBudgetKm),
                _ => throw new ArgumentException($"Invalid calculation type '{calculationType}'. Valid options: directions, matrix, range")
            };

    }, "CalculateRoute", new { calculationType, waypointCount = coordinates.Length, travelMode, avoidTolls, avoidHighways });
    }

    /// <summary>
    /// Route analysis for international travel and planning
    /// </summary>
    [Function(nameof(AnalyzeRoute))]
    public async Task<string> AnalyzeRoute(
        [McpToolTrigger(
            "navigation_analyze",
            "Analyze route for international travel, border crossings, and travel considerations."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "coordinates",
            "array",
            "Array of {latitude,longitude} waypoints to analyze. Min 2 points"
        )] LatLon[] coordinates
    )
    {
        return await ExecuteWithErrorHandling(async () =>
        {
            var coordsValidation = ValidationHelper.ValidateCoordinateArray(coordinates, c => (c.Latitude, c.Longitude), 2);
            if (!coordsValidation.IsValid) throw new ArgumentException(coordsValidation.ErrorMessage);

            _logger.LogInformation("Analyzing route with {Count} waypoints for international travel", coordinates.Length);

            var searchClient = _mapsService.SearchClient;
            var countriesFound = new HashSet<string>();
            var waypointAnalysis = new List<object>();

            // Analyze each waypoint for country information
            foreach (var (coord, index) in coordinates.Select((c, i) => (c, i)))
            {
                try
                {
                    var position = new GeoPosition(coord.Longitude, coord.Latitude);
                    var response = await searchClient.GetReverseGeocodingAsync(position);

                    string? countryCode = null;
                    string? countryName = null;
                    string? address = null;

                    if (response.Value?.Features?.Any() == true)
                    {
                        var feature = response.Value.Features.First();
                        var addressInfo = feature.Properties.Address;

                        countryCode = addressInfo?.CountryRegion?.Iso;
                        countryName = addressInfo?.CountryRegion?.Name;
                        address = addressInfo?.FormattedAddress;

                        if (!string.IsNullOrWhiteSpace(countryCode))
                        {
                            countriesFound.Add(countryCode);
                        }
                    }

                    waypointAnalysis.Add(new
                    {
                        waypointIndex = index,
                        coordinates = new { latitude = coord.Latitude, longitude = coord.Longitude },
                        countryCode,
                        countryName,
                        address
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to analyze waypoint {Index}", index);
                    waypointAnalysis.Add(new
                    {
                        waypointIndex = index,
                        coordinates = new { latitude = coord.Latitude, longitude = coord.Longitude },
                        error = ex.Message
                    });
                }
            }

            // Generate travel considerations
            var isInternational = countriesFound.Count > 1;
            object? travelConsiderations = null;

            if (isInternational)
            {
                travelConsiderations = new
                {
                    internationalTravel = true,
                    borderCrossings = countriesFound.Count - 1,
                    recommendedChecks = new[]
                    {
                        "Verify passport validity (6+ months remaining)",
                        "Check visa requirements for destination countries",
                        "Review customs regulations and duty-free allowances",
                        "Consider international driving permits if driving",
                        "Check currency exchange rates and payment methods",
                        "Verify international mobile/data roaming plans"
                    }
                };
            }

            return new
            {
                query = new { waypointCount = coordinates.Length },
                result = new
                {
                    summary = new
                    {
                        totalWaypoints = coordinates.Length,
                        countriesTraversed = countriesFound.Count,
                        countryCodes = countriesFound.ToArray(),
                        isInternationalRoute = isInternational
                    },
                    waypointAnalysis,
                    travelConsiderations
                }
            };

        }, "AnalyzeRoute", new { waypointCount = coordinates.Length });
    }

    #region Private Helper Methods

    private async Task<object> CalculateDirections(LatLon[] coordinates, TravelMode travelMode, RouteType routeType, bool avoidTolls, bool avoidHighways)
    {
        var routePoints = coordinates.Select(c => new GeoPosition(c.Longitude, c.Latitude)).ToList();

        var options = new RouteDirectionOptions()
        {
            TravelMode = travelMode,
            RouteType = routeType,
            UseTrafficData = true,
            ComputeBestWaypointOrder = routePoints.Count > 2,
            InstructionsType = RouteInstructionsType.Text
        };

        if (avoidTolls) options.Avoid.Add(RouteAvoidType.TollRoads);
        if (avoidHighways) options.Avoid.Add(RouteAvoidType.Motorways);

        var routeQuery = new RouteDirectionQuery(routePoints, options);
        var response = await _routingClient.GetDirectionsAsync(routeQuery);

        if (response.Value?.Routes?.Any() != true)
        {
            throw new InvalidOperationException("No route found between the specified points");
        }

        var route = response.Value.Routes.First();
        var geometry = route.Legs?.SelectMany(leg => leg.Points ?? new List<GeoPosition>())
            .Select(p => new[] { p.Latitude, p.Longitude }).ToList();

        return new
        {
            query = new { waypointCount = coordinates.Length, travelMode = travelMode.ToString(), routeType = routeType.ToString(), avoidTolls, avoidHighways },
            result = new
            {
                type = "directions",
                summary = new
                {
                    distanceMeters = route.Summary.LengthInMeters,
                    travelTimeSeconds = route.Summary.TravelTimeDuration?.TotalSeconds
                },
                geometry,
                legs = route.Legs?.Select(leg => new
                {
                    distanceMeters = leg.Summary.LengthInMeters,
                    travelTimeSeconds = leg.Summary.TravelTimeInSeconds,
                    trafficDelaySeconds = leg.Summary.TrafficDelayInSeconds
                }).ToList(),
                instructions = route.Guidance?.Instructions?.Select(i => new
                {
                    message = i.Message,
                    maneuver = i.Maneuver?.ToString(),
                    distanceMeters = i.RouteOffsetInMeters
                }).ToList()
            }
        };
    }

    private async Task<object> CalculateMatrix(LatLon[] coordinates, TravelMode travelMode, RouteType routeType)
    {
        // For matrix calculation, treat all points as both origins and destinations
        var points = coordinates.Select(c => new GeoPosition(c.Longitude, c.Latitude)).ToList();

        var matrixQuery = new RouteMatrixQuery
        {
            Origins = points,
            Destinations = points
        };

        var options = new RouteMatrixOptions(matrixQuery)
        {
            TravelMode = travelMode,
            RouteType = routeType,
            UseTrafficData = true
        };

        var response = await _routingClient.GetImmediateRouteMatrixAsync(options);

        if (response.Value?.Matrix == null)
        {
            throw new InvalidOperationException("No matrix data returned");
        }

        var matrix = response.Value.Matrix;
        var results = new List<object>();

        for (int i = 0; i < matrix.Count; i++)
        {
            for (int j = 0; j < matrix[i].Count; j++)
            {
                var cell = matrix[i][j];
                if (cell.Summary != null)
                {
                    results.Add(new
                    {
                        originIndex = i,
                        destinationIndex = j,
                        distanceMeters = cell.Summary.LengthInMeters,
                        travelTimeSeconds = cell.Summary.TravelTimeInSeconds,
                        trafficDelaySeconds = cell.Summary.TrafficDelayInSeconds
                    });
                }
            }
        }

        return new
        {
            query = new { pointCount = points.Count, travelMode = travelMode.ToString(), routeType = routeType.ToString() },
            result = new
            {
                type = "matrix",
                pointCount = points.Count,
                totalPairs = results.Count,
                results
            }
        };
    }

    private async Task<object> CalculateRange(LatLon centerPoint, TravelMode travelMode, RouteType routeType, int? timeBudgetMinutes, int? distanceBudgetKm)
    {
        if (!timeBudgetMinutes.HasValue && !distanceBudgetKm.HasValue)
        {
            throw new ArgumentException("Either timeBudgetMinutes or distanceBudgetKm must be specified for range calculation");
        }

        if (timeBudgetMinutes.HasValue && distanceBudgetKm.HasValue)
        {
            throw new ArgumentException("Specify either timeBudgetMinutes or distanceBudgetKm, not both");
        }

        var center = new GeoPosition(centerPoint.Longitude, centerPoint.Latitude);
        var options = new RouteRangeOptions(center)
        {
            TravelMode = travelMode,
            RouteType = routeType,
            UseTrafficData = true
        };

        if (timeBudgetMinutes.HasValue)
        {
            options.TimeBudget = TimeSpan.FromMinutes(timeBudgetMinutes.Value);
        }
        else if (distanceBudgetKm.HasValue)
        {
            options.DistanceBudgetInMeters = distanceBudgetKm.Value * 1000;
        }

        var response = await _routingClient.GetRouteRangeAsync(options);

        if (response.Value?.ReachableRange == null)
        {
            throw new InvalidOperationException("No reachable range data returned");
        }

        var range = response.Value.ReachableRange;
        var boundary = range.Boundary?.Select(p => new[] { p.Latitude, p.Longitude }).ToList();

        return new
        {
            query = new { center = new { latitude = centerPoint.Latitude, longitude = centerPoint.Longitude }, travelMode = travelMode.ToString(), routeType = routeType.ToString(), timeBudgetMinutes, distanceBudgetKm },
            result = new
            {
                type = "range",
                center = new[] { range.Center.Latitude, range.Center.Longitude },
                budget = timeBudgetMinutes.HasValue
                    ? (object)new { timeMinutes = timeBudgetMinutes.Value }
                    : new { distanceKm = distanceBudgetKm!.Value },
                boundary,
                boundaryPointCount = boundary?.Count ?? 0
            }
        };
    }

    private async Task<object> CalculateMatrixForOriginsDestinations(LatLon[] origins, LatLon[] destinations, TravelMode travelMode, RouteType routeType)
    {
        var originPoints = origins.Select(c => new GeoPosition(c.Longitude, c.Latitude)).ToList();
        var destinationPoints = destinations.Select(c => new GeoPosition(c.Longitude, c.Latitude)).ToList();

        var matrixQuery = new RouteMatrixQuery
        {
            Origins = originPoints,
            Destinations = destinationPoints
        };

        var options = new RouteMatrixOptions(matrixQuery)
        {
            TravelMode = travelMode,
            RouteType = routeType,
            UseTrafficData = true
        };

        var response = await _routingClient.GetImmediateRouteMatrixAsync(options);
        if (response.Value?.Matrix == null)
        {
            throw new InvalidOperationException("No matrix data returned");
        }

        var matrix = response.Value.Matrix;
        var results = new List<object>();

        for (int i = 0; i < matrix.Count; i++)
        {
            for (int j = 0; j < matrix[i].Count; j++)
            {
                var cell = matrix[i][j];
                if (cell.Summary != null)
                {
                    results.Add(new
                    {
                        originIndex = i,
                        destinationIndex = j,
                        distanceMeters = cell.Summary.LengthInMeters,
                        travelTimeSeconds = cell.Summary.TravelTimeInSeconds,
                        trafficDelaySeconds = cell.Summary.TrafficDelayInSeconds
                    });
                }
            }
        }

        return new
        {
            type = "matrix",
            originCount = originPoints.Count,
            destinationCount = destinationPoints.Count,
            totalPairs = results.Count,
            results
        };
    }

    #endregion
}