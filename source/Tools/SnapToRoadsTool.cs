// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using Azure.Maps.Mcp.Common;
using Azure.Maps.Mcp.Common.Models;
using Azure.Maps.Mcp.Services;

namespace Azure.Maps.Mcp.Tools;

/// <summary>
/// Snap To Roads tool using Azure Maps REST API (no SDK available for this API)
/// </summary>
public sealed class SnapToRoadsTool : BaseMapsTool
{
    private readonly AtlasRestClient _restClient;

    public SnapToRoadsTool(
        IAzureMapsService mapsService,
        ILogger<SnapToRoadsTool> logger,
        AtlasRestClient restClient)
        : base(mapsService, logger)
    {
        _restClient = restClient;
    }

    /// <summary>
    /// Snap GPS points to the nearest roads and optionally interpolate and include speed limits.
    /// </summary>
    [Function(nameof(SnapToRoads))]
    public Task<string> SnapToRoads(
        [McpToolTrigger(
            "snap_to_roads",
            "Snap GPS points to the road network; returns snapped/interpolated points with road names and optional speed limits.")]
        ToolInvocationContext context,
        [McpToolProperty(
            "points",
            "array",
            "Array of {latitude,longitude} GPS points. Min 2, max 100; consecutive points must be within 6 km.")]
        LatLon[] points,
        [McpToolProperty(
            "includeSpeedLimit",
            "boolean",
            "Include speed limit in km/h. Default: false")]
        bool includeSpeedLimit = false,
        [McpToolProperty(
            "interpolate",
            "boolean",
            "Interpolate additional points to smooth the path. Default: false")]
        bool interpolate = false,
        [McpToolProperty(
            "travelMode",
            "string",
            "Routing profile for snapping: 'driving' or 'truck'. Default: driving")]
        string travelMode = "driving")
    {
        return ExecuteWithErrorHandling(async () =>
        {
            // Validate input points
            var coordsValidation = ValidationHelper.ValidateCoordinateArray(points, p => (p.Latitude, p.Longitude), 2);
            if (!coordsValidation.IsValid)
                throw new ArgumentException(coordsValidation.ErrorMessage!, nameof(points));

            if (points.Length > 100)
                throw new ArgumentException("A maximum of 100 points are allowed", nameof(points));

            // Validate consecutive distance (<= 6 km) – quick client-side check
            var farIndex = FindFirstConsecutiveDistanceExceeding(points, 6.0);
            if (farIndex >= 0)
                throw new ArgumentException($"Consecutive points {farIndex} and {farIndex + 1} are more than 6 km apart", nameof(points));

            // Validate booleans
            // booleans already typed

            // Validate travel mode (Snap To Roads supports driving | truck)
            var validTravelModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "driving", "truck" };
            var mode = string.IsNullOrWhiteSpace(travelMode) ? "driving" : travelMode.Trim();
            if (!validTravelModes.Contains(mode))
                throw new ArgumentException("Invalid travelMode. Use 'driving' or 'truck'", nameof(travelMode));

            // Build GeoJSON FeatureCollection body
            var features = points.Select(p => new
            {
                type = "Feature",
                geometry = new
                {
                    type = "Point",
                    coordinates = new[] { p.Longitude, p.Latitude }
                },
                properties = new { }
            }).ToList();

            var body = new
            {
                type = "FeatureCollection",
                features,
                includeSpeedLimit,
                interpolate,
                travelMode = mode
            };

            var json = JsonSerializer.Serialize(body);

            _logger.LogInformation("Posting SnapToRoads with {Count} points, mode={Mode}, speedLimit={SL}, interpolate={Interp}", points.Length, mode, includeSpeedLimit, interpolate);

            var (ok, respBody, status, reason) = await _restClient.PostJsonAsync("route/snapToRoads?api-version=2025-01-01", json, "application/geo+json");
            if (!ok)
            {
                _logger.LogWarning("SnapToRoads API returned {Status}: {Body}", status, respBody);
                throw new Azure.RequestFailedException(status, $"SnapToRoads API error: {status} {reason}", new Exception(respBody));
            }

            using var doc = JsonDocument.Parse(respBody);
            var root = doc.RootElement;
            var summary = BuildSummary(root, points.Length);
            var simplifiedPoints = ExtractPoints(root);

            return new
            {
                query = new
                {
                    pointCount = points.Length,
                    includeSpeedLimit,
                    interpolate,
                    travelMode = mode
                },
                summary,
                points = simplifiedPoints
            };
        }, nameof(SnapToRoads), new { pointCount = points?.Length ?? 0, includeSpeedLimit, interpolate, travelMode });
    }

    private static object? SafeParse(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            return d.RootElement.Clone();
        }
        catch
        {
            return json;
        }
    }

    private static object BuildSummary(JsonElement root, int inputCount)
    {
        try
        {
            if (!root.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
            {
                return new { totalReturned = 0, snappedCount = 0, interpolatedCount = 0, matchedInputPoints = 0, matchRate = 0.0 };
            }

            int total = features.GetArrayLength();
            int snapped = 0;
            int interpolated = 0;
            var seenIndices = new HashSet<int>();

            foreach (var f in features.EnumerateArray())
            {
                if (f.TryGetProperty("properties", out var props))
                {
                    bool isInterp = props.TryGetProperty("isInterpolated", out var ii) && ii.ValueKind == JsonValueKind.True;
                    if (isInterp) interpolated++;
                    else snapped++;

                    if (props.TryGetProperty("inputIndex", out var idx) && idx.ValueKind == JsonValueKind.Number && idx.TryGetInt32(out var index))
                    {
                        seenIndices.Add(index);
                    }
                }
            }

            int matched = seenIndices.Count;
            double rate = inputCount > 0 ? Math.Round((double)matched / inputCount * 100, 1) : 0.0;

            return new
            {
                totalReturned = total,
                snappedCount = snapped,
                interpolatedCount = interpolated,
                matchedInputPoints = matched,
                matchRate = rate
            };
        }
        catch
        {
            return new { };
        }
    }

    private static List<object> ExtractPoints(JsonElement root)
    {
        var result = new List<object>();
        try
        {
            if (!root.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var f in features.EnumerateArray())
            {
                double? lat = null;
                double? lon = null;
                string? name = null;
                int? inputIndex = null;
                bool isInterpolated = false;
                double? speedKph = null;

                if (f.TryGetProperty("geometry", out var geom) &&
                    geom.TryGetProperty("coordinates", out var coords) &&
                    coords.ValueKind == JsonValueKind.Array && coords.GetArrayLength() >= 2)
                {
                    // coordinates: [lon, lat]
                    if (coords[0].TryGetDouble(out var lonVal)) lon = lonVal;
                    if (coords[1].TryGetDouble(out var latVal)) lat = latVal;
                }

                if (f.TryGetProperty("properties", out var props))
                {
                    if (props.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String) name = n.GetString();
                    if (props.TryGetProperty("isInterpolated", out var ii) && ii.ValueKind == JsonValueKind.True) isInterpolated = true;
                    if (props.TryGetProperty("inputIndex", out var idx) && idx.ValueKind == JsonValueKind.Number && idx.TryGetInt32(out var i)) inputIndex = i;
                    if (props.TryGetProperty("speedLimitInKilometersPerHour", out var sp))
                    {
                        if (sp.ValueKind == JsonValueKind.Number && sp.TryGetDouble(out var kph)) speedKph = kph;
                    }
                }

                result.Add(new
                {
                    latitude = lat,
                    longitude = lon,
                    roadName = name,
                    isInterpolated,
                    inputIndex,
                    speedLimitKph = speedKph
                });
            }
        }
        catch
        {
            // ignore
        }

        return result;
    }

    // Returns the index of the first pair violating maxDistanceKm, or -1 if all ok
    private static int FindFirstConsecutiveDistanceExceeding(LatLon[] points, double maxDistanceKm)
    {
        for (int i = 0; i < points.Length - 1; i++)
        {
            var d = HaversineDistanceKm(points[i].Latitude, points[i].Longitude, points[i + 1].Latitude, points[i + 1].Longitude);
            if (d > maxDistanceKm) return i;
        }
        return -1;
    }

    private static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0; // Earth radius in km
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRadians(double deg) => deg * Math.PI / 180.0;
}
