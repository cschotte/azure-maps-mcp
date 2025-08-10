// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using Azure.Maps.Mcp.Common;

namespace Azure.Maps.Mcp.Tools;

/// <summary>
/// Streamlined Places tool covering the most common scenarios with minimal inputs.
/// - places_nearby: simple nearby search by coordinates and optional radius
/// - places_search: free text search with optional lat/lon bias
/// - places_categories: fetch category tree
/// All endpoints return a consistent shape: { query, items[], summary }
/// </summary>
public sealed class PlacesTool : BaseMapsTool
{
    private readonly AtlasRestClient _atlas;

    public PlacesTool(AtlasRestClient atlas, Services.IAzureMapsService mapsService, ILogger<PlacesTool> logger)
        : base(mapsService, logger)
    {
        _atlas = atlas;
    }

    [Function(nameof(Nearby))]
    public async Task<string> Nearby(
        [McpToolTrigger(
            "places_nearby",
            "Find popular places near coordinates. Minimal inputs; sensible defaults.")]
        ToolInvocationContext context,
        [McpToolProperty("latitude", "number", "Latitude (-90..90)")] double latitude,
        [McpToolProperty("longitude", "number", "Longitude (-180..180)")] double longitude,
        [McpToolProperty("radiusMeters", "number", "Optional radius meters (100..20000). Default 2000")] int? radiusMeters = 2000,
        [McpToolProperty("limit", "number", "Optional max results (1..25). Default 10")] int? limit = 10
    )
    {
        var coordErr = ValidateCoordinates(latitude, longitude);
        if (coordErr != null) return coordErr;

        if (radiusMeters.HasValue)
        {
            var (err, norm) = ValidateRange(radiusMeters.Value, 100, 20000, nameof(radiusMeters));
            if (err != null) return err;
            radiusMeters = norm;
        }

        if (limit.HasValue)
        {
            var (err, norm) = ValidateRange(limit.Value, 1, 25, nameof(limit));
            if (err != null) return err;
            limit = norm;
        }

        return await ExecuteWithErrorHandling(async () =>
        {
            var query = new Dictionary<string, string?>
            {
                { "api-version", "1.0" },
                { "lat", latitude.ToString(CultureInfo.InvariantCulture) },
                { "lon", longitude.ToString(CultureInfo.InvariantCulture) },
                { "radius", radiusMeters?.ToString(CultureInfo.InvariantCulture) },
                { "limit", limit?.ToString(CultureInfo.InvariantCulture) }
            };

            var (ok, body, status, reason) = await _atlas.GetAsync("search/nearby/json", query);
            if (!ok) throw new InvalidOperationException($"Nearby search failed: {status} {reason}");

            using var doc = JsonDocument.Parse(body);
            return new
            {
                query = new { latitude, longitude, radiusMeters, limit },
                items = ExtractItems(doc.RootElement),
                summary = ExtractSummary(doc.RootElement)
            };
        }, "Places.Nearby", new { latitude, longitude, radiusMeters, limit });
    }

    [Function(nameof(Search))]
    public async Task<string> Search(
        [McpToolTrigger(
            "places_search",
            "Search places by name/keywords with optional location bias.")]
        ToolInvocationContext context,
        [McpToolProperty("query", "string", "Search text, e.g. 'coffee', 'pharmacy 98052'")] string queryText,
        [McpToolProperty("latitude", "number", "Optional bias latitude")] double? latitude = null,
        [McpToolProperty("longitude", "number", "Optional bias longitude")] double? longitude = null,
        [McpToolProperty("limit", "number", "Optional max results (1..25). Default 10")] int? limit = 10
    )
    {
        var qerr = ValidateStringInput(queryText, 2, 256, nameof(queryText));
        if (qerr != null) return qerr;

        if ((latitude.HasValue || longitude.HasValue) && (!latitude.HasValue || !longitude.HasValue))
            return ResponseHelper.CreateValidationError("Provide both latitude and longitude for bias");

        if (latitude.HasValue && longitude.HasValue)
        {
            var cerr = ValidateCoordinates(latitude.Value, longitude.Value);
            if (cerr != null) return cerr;
        }

        if (limit.HasValue)
        {
            var (err, norm) = ValidateRange(limit.Value, 1, 25, nameof(limit));
            if (err != null) return err;
            limit = norm;
        }

        return await ExecuteWithErrorHandling(async () =>
        {
            var query = new Dictionary<string, string?>
            {
                { "api-version", "1.0" },
                { "query", queryText.Trim() },
                { "lat", latitude?.ToString(CultureInfo.InvariantCulture) },
                { "lon", longitude?.ToString(CultureInfo.InvariantCulture) },
                { "limit", limit?.ToString(CultureInfo.InvariantCulture) }
            };

            var (ok, body, status, reason) = await _atlas.GetAsync("search/fuzzy/json", query);
            if (!ok) throw new InvalidOperationException($"Search failed: {status} {reason}");

            using var doc = JsonDocument.Parse(body);
            return new
            {
                query = new { query = queryText.Trim(), latitude, longitude, limit },
                items = ExtractItems(doc.RootElement),
                summary = ExtractSummary(doc.RootElement)
            };
        }, "Places.Search", new { queryText, latitude, longitude, limit });
    }

    [Function(nameof(Categories))]
    public async Task<string> Categories(
        [McpToolTrigger(
            "places_categories",
            "Get the POI category tree (IDs, names).")]
        ToolInvocationContext context
    )
    {
        return await ExecuteWithErrorHandling(async () =>
        {
            var (ok, body, status, reason) = await _atlas.GetAsync("search/poi/category/tree/json", new Dictionary<string, string?>
            {
                { "api-version", "1.0" }
            });
            if (!ok) throw new InvalidOperationException($"Category tree failed: {status} {reason}");

            using var doc = JsonDocument.Parse(body);
            int total = 0;
            try
            {
                if (doc.RootElement.TryGetProperty("poiCategories", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    total = arr.GetArrayLength();
            }
            catch { }

            return new { summary = new { total } , raw = doc.RootElement };
        }, "Places.Categories");
    }

    private static IEnumerable<object> ExtractItems(JsonElement root)
    {
        var list = new List<object>();
        try
        {
            if (!root.TryGetProperty("results", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var r in arr.EnumerateArray())
            {
                string? name = r.TryGetProperty("poi", out var poi) && poi.TryGetProperty("name", out var nm) ? nm.GetString() : null;
                string? address = r.TryGetProperty("address", out var addr) && addr.TryGetProperty("freeformAddress", out var fa) ? fa.GetString() : null;
                double? lat = r.TryGetProperty("position", out var pos) && pos.TryGetProperty("lat", out var la) && la.ValueKind == JsonValueKind.Number ? la.GetDouble() : (double?)null;
                double? lon = r.TryGetProperty("position", out var pos2) && pos2.TryGetProperty("lon", out var lo) && lo.ValueKind == JsonValueKind.Number ? lo.GetDouble() : (double?)null;

                list.Add(new
                {
                    name,
                    address,
                    coordinates = (lat.HasValue && lon.HasValue) ? new { latitude = lat, longitude = lon } : null
                });
            }
        }
        catch { }
        return list;
    }

    private static object ExtractSummary(JsonElement root)
    {
        try
        {
            if (!root.TryGetProperty("summary", out var s)) return new { };
            return new
            {
                numResults = s.TryGetProperty("numResults", out var nr) ? nr.GetInt32() : (int?)null,
                offset = s.TryGetProperty("offset", out var off) ? off.GetInt32() : (int?)null,
                totalResults = s.TryGetProperty("totalResults", out var tr) ? tr.GetInt32() : (int?)null
            };
        }
        catch { return new { }; }
    }
}
