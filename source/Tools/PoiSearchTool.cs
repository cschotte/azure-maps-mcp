// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Globalization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Maps.Mcp.Common;
using Azure.Maps.Mcp.Services;

namespace Azure.Maps.Mcp.Tools;

/// <summary>
/// Points of Interest (POI) search tool using Azure Maps REST APIs (no SDK for some APIs)
/// Implements: Get Search POI, Get Search POI Category, Get Search POI Category Tree,
/// Get Search Nearby, Get Search Fuzzy.
/// </summary>
public sealed class PoiSearchTool : BaseMapsTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _subscriptionKey;

    public PoiSearchTool(
        IAzureMapsService mapsService,
        ILogger<PoiSearchTool> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
        : base(mapsService, logger)
    {
        _httpClientFactory = httpClientFactory;
        _subscriptionKey =
            configuration["AZURE_MAPS_SUBSCRIPTION_KEY"] ??
            configuration["Values:AZURE_MAPS_SUBSCRIPTION_KEY"] ??
            throw new InvalidOperationException("AZURE_MAPS_SUBSCRIPTION_KEY is required for POI Search API calls");
    }

    // =============== Get Search POI (by name) ===============
    [Function(nameof(GetSearchPoi))]
    public async Task<string> GetSearchPoi(
        [McpToolTrigger(
            "search_poi",
            "Search points of interest (POI) by name, optionally biased or filtered by location, brand, category, or country.")]
        ToolInvocationContext context,
        [McpToolProperty("query", "string", "POI name or token, e.g. 'Starbucks'.")] string query,
        [McpToolProperty("lat", "number", "Optional: latitude to bias or constrain results.")] double? lat = null,
        [McpToolProperty("lon", "number", "Optional: longitude to bias or constrain results.")] double? lon = null,
        [McpToolProperty("radius", "number", "Optional: search radius in meters (1-50000) when lat/lon provided.")] int? radius = null,
        [McpToolProperty("limit", "number", "Optional: max results (1-100). Default 10.")] int? limit = 10,
        [McpToolProperty("ofs", "number", "Optional: offset (0-1900). Default 0.")] int? ofs = 0,
        [McpToolProperty("categorySet", "string", "Optional: comma-separated category IDs (max 10). Example: '7315,7315017'.")] string? categorySet = null,
        [McpToolProperty("brandSet", "string", "Optional: comma-separated brand names. Names with commas should be quoted.")] string? brandSet = null,
        [McpToolProperty("countrySet", "string", "Optional: comma-separated 2-letter country/region codes, e.g. 'US,CA'.")] string? countrySet = null,
        [McpToolProperty("typeahead", "boolean", "Optional: treat query as partial input (predictive mode). Default false.")] bool? typeahead = null,
        [McpToolProperty("openingHours", "string", "Optional: include opening hours. Only 'nextSevenDays' is supported.")] string? openingHours = null,
        [McpToolProperty("language", "string", "Optional: IETF language tag for results, e.g. 'en-US'.")] string? language = null,
        [McpToolProperty("extendedPostalCodesFor", "string", "Optional: indexes for which to include extended postal codes, e.g. 'POI' or 'PAD,Addr,POI'.")] string? extendedPostalCodesFor = null,
        [McpToolProperty("connectorSet", "string", "Optional: EV connector filter, CSV of connector types.")] string? connectorSet = null,
        [McpToolProperty("view", "string", "Optional: localized map view region, e.g. 'Unified' or 'Auto'.")] string? view = null)
    {
        var err = ValidateStringInput(query, 1, 2048, nameof(query));
        if (err != null) return err;

        var range = ValidateCommonRanges(limit, ofs, radius, requireRadiusWithLatLon: lat.HasValue || lon.HasValue);
        if (range.error != null) return range.error;

        // If either lat or lon provided, require both and validate.
        if ((lat.HasValue || lon.HasValue) && (!lat.HasValue || !lon.HasValue))
        {
            return ResponseHelper.CreateValidationError("Both lat and lon must be provided together.");
        }
        if (lat.HasValue && lon.HasValue)
        {
            var coordErr = ValidateCoordinates(lat.Value, lon.Value);
            if (coordErr != null) return coordErr;
        }

        var catErr = ValidateCategorySet(categorySet);
        if (catErr != null) return catErr;
        var ctryErr = ValidateCountrySet(countrySet);
        if (ctryErr != null) return ctryErr;
        var ohErr = ValidateOpeningHours(openingHours);
        if (ohErr != null) return ohErr;

        try
        {
            var url = "search/poi/json";
            var q = BuildBaseQuery(query: query, lat: lat, lon: lon, radius: range.normalizedRadius,
                limit: range.normalizedLimit, ofs: range.normalizedOfs, categorySet: categorySet, countrySet: countrySet,
                language: language, extendedPostalCodesFor: extendedPostalCodesFor, brandSet: brandSet,
                connectorSet: connectorSet, typeahead: typeahead, openingHours: openingHours, view: view);
            var (ok, body, status, reason) = await SendGetAsync(url, q);
            if (!ok)
            {
                return ResponseHelper.CreateErrorResponse($"POI Search error: {status} {reason}", new { status, response = SafeParse(body) });
            }

            using var doc = JsonDocument.Parse(body);
            var result = new
            {
                query = new { query, lat, lon, radius = range.normalizedRadius, limit = range.normalizedLimit, ofs = range.normalizedOfs },
                summary = ExtractSearchSummary(doc.RootElement),
                items = ExtractSearchItems(doc.RootElement),
                raw = doc.RootElement
            };
            return ResponseHelper.CreateSuccessResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed POI search");
            return ResponseHelper.CreateErrorResponse("Failed to perform POI search");
        }
    }

    // =============== Get Search POI Category ===============
    [Function(nameof(GetSearchPoiCategory))]
    public async Task<string> GetSearchPoiCategory(
        [McpToolTrigger(
            "search_poi_category",
            "Search POIs by category name (e.g., 'Restaurant', 'ATM'), with optional filters and geo bias.")]
        ToolInvocationContext context,
        [McpToolProperty("query", "string", "Category name, e.g. 'Restaurant', 'ATM'.")] string query,
        [McpToolProperty("lat", "number", "Optional: latitude to bias or constrain results.")] double? lat = null,
        [McpToolProperty("lon", "number", "Optional: longitude to bias or constrain results.")] double? lon = null,
        [McpToolProperty("radius", "number", "Optional: search radius in meters (1-50000) when lat/lon provided.")] int? radius = null,
        [McpToolProperty("limit", "number", "Optional: max results (1-100). Default 10.")] int? limit = 10,
        [McpToolProperty("ofs", "number", "Optional: offset (0-1900). Default 0.")] int? ofs = 0,
        [McpToolProperty("categorySet", "string", "Optional: comma-separated category IDs (max 10). Example: '7315,7315017'.")] string? categorySet = null,
        [McpToolProperty("brandSet", "string", "Optional: comma-separated brand names.")] string? brandSet = null,
        [McpToolProperty("countrySet", "string", "Optional: comma-separated 2-letter country/region codes.")] string? countrySet = null,
        [McpToolProperty("typeahead", "boolean", "Optional: treat query as partial input (predictive mode). Default false.")] bool? typeahead = null,
        [McpToolProperty("openingHours", "string", "Optional: include opening hours. Only 'nextSevenDays' is supported.")] string? openingHours = null,
        [McpToolProperty("language", "string", "Optional: IETF language tag for results.")] string? language = null,
        [McpToolProperty("extendedPostalCodesFor", "string", "Optional: indexes for which to include extended postal codes.")] string? extendedPostalCodesFor = null,
        [McpToolProperty("connectorSet", "string", "Optional: EV connector filter, CSV of connector types.")] string? connectorSet = null,
        [McpToolProperty("view", "string", "Optional: localized map view region.")] string? view = null)
    {
        var err = ValidateStringInput(query, 1, 2048, nameof(query));
        if (err != null) return err;

        var range = ValidateCommonRanges(limit, ofs, radius, requireRadiusWithLatLon: lat.HasValue || lon.HasValue);
        if (range.error != null) return range.error;

        if ((lat.HasValue || lon.HasValue) && (!lat.HasValue || !lon.HasValue))
            return ResponseHelper.CreateValidationError("Both lat and lon must be provided together.");
        if (lat.HasValue && lon.HasValue)
        {
            var coordErr = ValidateCoordinates(lat.Value, lon.Value);
            if (coordErr != null) return coordErr;
        }

        var catErr = ValidateCategorySet(categorySet);
        if (catErr != null) return catErr;
        var ctryErr = ValidateCountrySet(countrySet);
        if (ctryErr != null) return ctryErr;
        var ohErr = ValidateOpeningHours(openingHours);
        if (ohErr != null) return ohErr;

        try
        {
            var url = "search/poi/category/json";
            var q = BuildBaseQuery(query: query, lat: lat, lon: lon, radius: range.normalizedRadius,
                limit: range.normalizedLimit, ofs: range.normalizedOfs, categorySet: categorySet, countrySet: countrySet,
                language: language, extendedPostalCodesFor: extendedPostalCodesFor, brandSet: brandSet,
                connectorSet: connectorSet, typeahead: typeahead, openingHours: openingHours, view: view);
            var (ok, body, status, reason) = await SendGetAsync(url, q);
            if (!ok)
            {
                return ResponseHelper.CreateErrorResponse($"POI Category Search error: {status} {reason}", new { status, response = SafeParse(body) });
            }

            using var doc = JsonDocument.Parse(body);
            var result = new
            {
                query = new { query, lat, lon, radius = range.normalizedRadius, limit = range.normalizedLimit, ofs = range.normalizedOfs },
                summary = ExtractSearchSummary(doc.RootElement),
                items = ExtractSearchItems(doc.RootElement),
                raw = doc.RootElement
            };
            return ResponseHelper.CreateSuccessResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed POI category search");
            return ResponseHelper.CreateErrorResponse("Failed to perform POI category search");
        }
    }

    // =============== Get Search POI Category Tree ===============
    [Function(nameof(GetPoiCategoryTree))]
    public async Task<string> GetPoiCategoryTree(
        [McpToolTrigger(
            "search_poi_category_tree",
            "Get the full POI category tree (IDs, names, childCategoryIds, synonyms).")]
        ToolInvocationContext context,
        [McpToolProperty("language", "string", "Optional: IETF language tag for localized category names, e.g. 'en-US'.")] string? language = null)
    {
        try
        {
            var url = "search/poi/category/tree/json";
            var q = new Dictionary<string, string?>
            {
                { "api-version", "1.0" },
                { "subscription-key", _subscriptionKey },
                { "language", NormalizeEmpty(language) }
            };
            var (ok, body, status, reason) = await SendGetAsync(url, q);
            if (!ok)
            {
                return ResponseHelper.CreateErrorResponse($"POI Category Tree error: {status} {reason}", new { status, response = SafeParse(body) });
            }
            using var doc = JsonDocument.Parse(body);
            var total = 0;
            try
            {
                if (doc.RootElement.TryGetProperty("poiCategories", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    total = arr.GetArrayLength();
                }
            }
            catch { /* ignore */ }

            var result = new
            {
                summary = new { totalCategories = total, language },
                raw = doc.RootElement
            };
            return ResponseHelper.CreateSuccessResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get POI category tree");
            return ResponseHelper.CreateErrorResponse("Failed to get POI category tree");
        }
    }

    // =============== Get Search Nearby ===============
    [Function(nameof(GetSearchNearby))]
    public async Task<string> GetSearchNearby(
        [McpToolTrigger(
            "search_nearby",
            "Search for POIs near a latitude/longitude, optionally filtered by category or brand.")]
        ToolInvocationContext context,
        [McpToolProperty("lat", "number", "Latitude (-90 to 90).")] double lat,
        [McpToolProperty("lon", "number", "Longitude (-180 to 180).")] double lon,
        [McpToolProperty("radius", "number", "Optional: search radius in meters (1-50000). Default depends on service.")] int? radius = null,
        [McpToolProperty("limit", "number", "Optional: max results (1-100). Default 10.")] int? limit = 10,
        [McpToolProperty("ofs", "number", "Optional: offset (0-1900). Default 0.")] int? ofs = 0,
        [McpToolProperty("categorySet", "string", "Optional: comma-separated category IDs (max 10).")] string? categorySet = null,
        [McpToolProperty("brandSet", "string", "Optional: comma-separated brand names.")] string? brandSet = null,
        [McpToolProperty("countrySet", "string", "Optional: comma-separated 2-letter country/region codes.")] string? countrySet = null,
        [McpToolProperty("language", "string", "Optional: IETF language tag for results.")] string? language = null,
        [McpToolProperty("extendedPostalCodesFor", "string", "Optional: indexes for which to include extended postal codes.")] string? extendedPostalCodesFor = null,
        [McpToolProperty("connectorSet", "string", "Optional: EV connector filter, CSV of connector types.")] string? connectorSet = null,
        [McpToolProperty("view", "string", "Optional: localized map view region.")] string? view = null)
    {
        var coordErr = ValidateCoordinates(lat, lon);
        if (coordErr != null) return coordErr;
        var range = ValidateCommonRanges(limit, ofs, radius, requireRadiusWithLatLon: false, nearbyMode: true);
        if (range.error != null) return range.error;
        var catErr = ValidateCategorySet(categorySet);
        if (catErr != null) return catErr;
        var ctryErr = ValidateCountrySet(countrySet);
        if (ctryErr != null) return ctryErr;

        try
        {
            var url = "search/nearby/json";
            var q = BuildBaseQuery(query: null, lat: lat, lon: lon, radius: range.normalizedRadius,
                limit: range.normalizedLimit, ofs: range.normalizedOfs, categorySet: categorySet, countrySet: countrySet,
                language: language, extendedPostalCodesFor: extendedPostalCodesFor, brandSet: brandSet,
                connectorSet: connectorSet, typeahead: null, openingHours: null, view: view);
            var (ok, body, status, reason) = await SendGetAsync(url, q);
            if (!ok)
            {
                return ResponseHelper.CreateErrorResponse($"Nearby Search error: {status} {reason}", new { status, response = SafeParse(body) });
            }

            using var doc = JsonDocument.Parse(body);
            var result = new
            {
                query = new { lat, lon, radius = range.normalizedRadius, limit = range.normalizedLimit, ofs = range.normalizedOfs },
                summary = ExtractSearchSummary(doc.RootElement),
                items = ExtractSearchItems(doc.RootElement),
                raw = doc.RootElement
            };
            return ResponseHelper.CreateSuccessResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed nearby search");
            return ResponseHelper.CreateErrorResponse("Failed to perform nearby search");
        }
    }

    // =============== Get Search Fuzzy ===============
    [Function(nameof(GetSearchFuzzy))]
    public async Task<string> GetSearchFuzzy(
        [McpToolTrigger(
            "search_fuzzy",
            "Free-form search mixing POI and address tokens. Supports geo biasing, radius, and advanced options.")]
        ToolInvocationContext context,
        [McpToolProperty("query", "string", "Free-form query: POI and/or address tokens, e.g. 'pizza 98052'.")] string query,
        [McpToolProperty("lat", "number", "Optional: latitude to bias results.")] double? lat = null,
        [McpToolProperty("lon", "number", "Optional: longitude to bias results.")] double? lon = null,
        [McpToolProperty("radius", "number", "Optional: search radius in meters (1-50000).")] int? radius = null,
        [McpToolProperty("limit", "number", "Optional: max results (1-100). Default 10.")] int? limit = 10,
        [McpToolProperty("ofs", "number", "Optional: offset (0-1900). Default 0.")] int? ofs = 0,
        [McpToolProperty("categorySet", "string", "Optional: comma-separated category IDs (max 10).")] string? categorySet = null,
        [McpToolProperty("brandSet", "string", "Optional: comma-separated brand names.")] string? brandSet = null,
        [McpToolProperty("countrySet", "string", "Optional: comma-separated 2-letter country/region codes.")] string? countrySet = null,
        [McpToolProperty("typeahead", "boolean", "Optional: treat query as partial input (predictive mode). Default false.")] bool? typeahead = null,
        [McpToolProperty("openingHours", "string", "Optional: include opening hours. Only 'nextSevenDays' is supported.")] string? openingHours = null,
        [McpToolProperty("language", "string", "Optional: IETF language tag for results.")] string? language = null,
        [McpToolProperty("extendedPostalCodesFor", "string", "Optional: indexes for which to include extended postal codes.")] string? extendedPostalCodesFor = null,
        [McpToolProperty("minFuzzyLevel", "number", "Optional: 1-4. Default 1.")] int? minFuzzyLevel = null,
        [McpToolProperty("maxFuzzyLevel", "number", "Optional: 1-4. Default 2.")] int? maxFuzzyLevel = null,
        [McpToolProperty("idxSet", "string", "Optional: indexes to use, CSV of {Addr,Geo,PAD,POI,Str,Xstr}.")] string? idxSet = null,
        [McpToolProperty("entityType", "string", "Optional: filter geographies to a type, e.g. 'Municipality'.")] string? entityType = null,
        [McpToolProperty("topLeft", "string", "Optional: bbox top-left 'lat,lon'.")] string? topLeft = null,
        [McpToolProperty("btmRight", "string", "Optional: bbox bottom-right 'lat,lon'.")] string? btmRight = null,
        [McpToolProperty("connectorSet", "string", "Optional: EV connector filter, CSV of connector types.")] string? connectorSet = null,
        [McpToolProperty("view", "string", "Optional: localized map view region.")] string? view = null)
    {
        var err = ValidateStringInput(query, 1, 2048, nameof(query));
        if (err != null) return err;

        // If either lat or lon provided, require both and validate.
        if ((lat.HasValue || lon.HasValue) && (!lat.HasValue || !lon.HasValue))
            return ResponseHelper.CreateValidationError("Both lat and lon must be provided together.");
        if (lat.HasValue && lon.HasValue)
        {
            var coordErr = ValidateCoordinates(lat.Value, lon.Value);
            if (coordErr != null) return coordErr;
        }

        var range = ValidateCommonRanges(limit, ofs, radius, requireRadiusWithLatLon: false);
        if (range.error != null) return range.error;

        var catErr = ValidateCategorySet(categorySet);
        if (catErr != null) return catErr;
        var ctryErr = ValidateCountrySet(countrySet);
        if (ctryErr != null) return ctryErr;
        var ohErr = ValidateOpeningHours(openingHours);
        if (ohErr != null) return ohErr;
        var fuzzyErr = ValidateFuzzyLevels(minFuzzyLevel, maxFuzzyLevel);
        if (fuzzyErr != null) return fuzzyErr;
        var idxErr = ValidateIdxSet(idxSet);
        if (idxErr != null) return idxErr;
        var bboxErr = ValidateBoundingBox(topLeft, btmRight);
        if (bboxErr != null) return bboxErr;

        if (!string.IsNullOrWhiteSpace(entityType) && !ValidEntityTypes.Contains(entityType.Trim()))
        {
            return ResponseHelper.CreateValidationError("Invalid entityType. See docs for allowed values (e.g., Country, Municipality, PostalCodeArea).");
        }

        try
        {
            var url = "search/fuzzy/json";
            var q = BuildBaseQuery(query: query, lat: lat, lon: lon, radius: range.normalizedRadius,
                limit: range.normalizedLimit, ofs: range.normalizedOfs, categorySet: categorySet, countrySet: countrySet,
                language: language, extendedPostalCodesFor: extendedPostalCodesFor, brandSet: brandSet,
                connectorSet: connectorSet, typeahead: typeahead, openingHours: openingHours, view: view);
            // Additional fuzzy-only params
            q["minFuzzyLevel"] = minFuzzyLevel?.ToString(CultureInfo.InvariantCulture);
            q["maxFuzzyLevel"] = maxFuzzyLevel?.ToString(CultureInfo.InvariantCulture);
            q["idxSet"] = NormalizeEmpty(idxSet);
            q["entityType"] = NormalizeEmpty(entityType);
            q["topLeft"] = NormalizeEmpty(topLeft);
            q["btmRight"] = NormalizeEmpty(btmRight);

            var (ok, body, status, reason) = await SendGetAsync(url, q);
            if (!ok)
            {
                return ResponseHelper.CreateErrorResponse($"Fuzzy Search error: {status} {reason}", new { status, response = SafeParse(body) });
            }

            using var doc = JsonDocument.Parse(body);
            var result = new
            {
                query = new
                {
                    query,
                    lat,
                    lon,
                    radius = range.normalizedRadius,
                    limit = range.normalizedLimit,
                    ofs = range.normalizedOfs,
                    minFuzzyLevel,
                    maxFuzzyLevel,
                    idxSet,
                    entityType
                },
                summary = ExtractSearchSummary(doc.RootElement),
                items = ExtractSearchItems(doc.RootElement),
                raw = doc.RootElement
            };
            return ResponseHelper.CreateSuccessResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed fuzzy search");
            return ResponseHelper.CreateErrorResponse("Failed to perform fuzzy search");
        }
    }

    // ---------------- helpers ----------------
    private async Task<(bool ok, string body, int status, string? reason)> SendGetAsync(string path, IDictionary<string, string?> query)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://atlas.microsoft.com/");

        // Trim null/empty
        var q = query
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => $"{kv.Key}={WebUtility.UrlEncode(kv.Value)}");
        var url = $"{path}?{string.Join('&', q)}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("Accept", "application/json");

        using var resp = await client.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        return (resp.IsSuccessStatusCode, body, (int)resp.StatusCode, resp.ReasonPhrase);
    }

    private Dictionary<string, string?> BuildBaseQuery(
        string? query,
        double? lat,
        double? lon,
        int? radius,
        int? limit,
        int? ofs,
        string? categorySet,
        string? countrySet,
        string? language,
        string? extendedPostalCodesFor,
        string? brandSet,
        string? connectorSet,
        bool? typeahead,
        string? openingHours,
        string? view)
    {
        var q = new Dictionary<string, string?>
        {
            { "api-version", "1.0" },
            { "subscription-key", _subscriptionKey },
            { "query", NormalizeEmpty(query) },
            { "lat", lat?.ToString(CultureInfo.InvariantCulture) },
            { "lon", lon?.ToString(CultureInfo.InvariantCulture) },
            { "radius", radius?.ToString(CultureInfo.InvariantCulture) },
            { "limit", limit?.ToString(CultureInfo.InvariantCulture) },
            { "ofs", ofs?.ToString(CultureInfo.InvariantCulture) },
            { "categorySet", NormalizeEmpty(NormalizeCsv(categorySet)) },
            { "countrySet", NormalizeEmpty(NormalizeCountryCsv(countrySet)) },
            { "language", NormalizeEmpty(language) },
            { "extendedPostalCodesFor", NormalizeEmpty(NormalizeCsv(extendedPostalCodesFor)) },
            { "brandSet", NormalizeEmpty(categorySet: false, value: brandSet) },
            { "connectorSet", NormalizeEmpty(NormalizeCsv(connectorSet)) },
            { "typeahead", typeahead.HasValue ? (typeahead.Value ? "true" : "false") : null },
            { "openingHours", NormalizeEmpty(openingHours) },
            { "view", NormalizeEmpty(view) }
        };
        return q;
    }

    private static object ExtractSearchSummary(JsonElement root)
    {
        try
        {
            if (!root.TryGetProperty("summary", out var s)) return new { };
            var obj = new
            {
                query = s.TryGetProperty("query", out var qv) ? qv.GetString() : null,
                queryType = s.TryGetProperty("queryType", out var qt) ? qt.GetString() : null,
                numResults = s.TryGetProperty("numResults", out var nr) ? nr.GetInt32() : 0,
                totalResults = s.TryGetProperty("totalResults", out var tr) ? tr.GetInt32() : (int?)null,
                offset = s.TryGetProperty("offset", out var off) ? off.GetInt32() : 0,
                limit = s.TryGetProperty("limit", out var lim) ? lim.GetInt32() : (int?)null,
                fuzzyLevel = s.TryGetProperty("fuzzyLevel", out var fl) ? fl.GetInt32() : (int?)null,
                geoBias = s.TryGetProperty("geoBias", out var gb) ? gb : default
            };
            return obj;
        }
        catch
        {
            return new { };
        }
    }

    private static IEnumerable<object> ExtractSearchItems(JsonElement root)
    {
        var list = new List<object>();
        try
        {
            if (!root.TryGetProperty("results", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var r in arr.EnumerateArray())
            {
                string? type = r.TryGetProperty("type", out var t) ? t.GetString() : null;
                string? id = r.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                double? score = r.TryGetProperty("score", out var sc) && sc.ValueKind == JsonValueKind.Number ? sc.GetDouble() : (double?)null;
                double? dist = r.TryGetProperty("dist", out var di) && di.ValueKind == JsonValueKind.Number ? di.GetDouble() : (double?)null;

                // poi
                string? name = null;
                string[]? categories = null;
                int[]? categoryIds = null;
                string? phone = null;
                string? url = null;
                string[]? brands = null;
                if (r.TryGetProperty("poi", out var poi) && poi.ValueKind == JsonValueKind.Object)
                {
                    name = poi.TryGetProperty("name", out var nm) ? nm.GetString() : null;
                    if (poi.TryGetProperty("categories", out var catArr) && catArr.ValueKind == JsonValueKind.Array)
                    {
                        categories = catArr.EnumerateArray()
                            .Where(e => e.ValueKind == JsonValueKind.String)
                            .Select(e => e.GetString()!)
                            .ToArray();
                    }
                    if (poi.TryGetProperty("categorySet", out var csArr) && csArr.ValueKind == JsonValueKind.Array)
                    {
                        categoryIds = csArr.EnumerateArray()
                            .Select(e => e.TryGetProperty("id", out var id2) && id2.ValueKind == JsonValueKind.Number ? id2.GetInt32() : (int?)null)
                            .Where(v => v.HasValue)
                            .Select(v => v!.Value)
                            .ToArray();
                    }
                    phone = poi.TryGetProperty("phone", out var ph) ? ph.GetString() : null;
                    url = poi.TryGetProperty("url", out var u) ? u.GetString() : null;
                    if (poi.TryGetProperty("brands", out var brArr) && brArr.ValueKind == JsonValueKind.Array)
                    {
                        brands = brArr.EnumerateArray()
                            .Select(e => e.TryGetProperty("name", out var bn) ? bn.GetString() : null)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => s!)
                            .ToArray();
                    }
                }

                // address
                string? freeformAddress = null;
                if (r.TryGetProperty("address", out var addr) && addr.ValueKind == JsonValueKind.Object)
                {
                    freeformAddress = addr.TryGetProperty("freeformAddress", out var fa) ? fa.GetString() : null;
                }

                // position
                double? plat = null, plon = null;
                if (r.TryGetProperty("position", out var pos) && pos.ValueKind == JsonValueKind.Object)
                {
                    plat = pos.TryGetProperty("lat", out var la) && la.ValueKind == JsonValueKind.Number ? la.GetDouble() : (double?)null;
                    plon = pos.TryGetProperty("lon", out var lo) && lo.ValueKind == JsonValueKind.Number ? lo.GetDouble() : (double?)null;
                }

                list.Add(new
                {
                    id,
                    type,
                    score,
                    dist,
                    name,
                    categories,
                    categoryIds,
                    phone,
                    url,
                    address = freeformAddress,
                    position = (plat.HasValue && plon.HasValue) ? new { lat = plat, lon = plon } : null
                });
            }
        }
        catch
        {
            // ignore
        }
        return list;
    }

    private (string? error, int? normalizedLimit, int? normalizedOfs, int? normalizedRadius) ValidateCommonRanges(
        int? limit, int? ofs, int? radius, bool requireRadiusWithLatLon, bool nearbyMode = false)
    {
        // limit (1-100)
        if (limit.HasValue)
        {
            var (err, norm) = ValidateRange(limit.Value, 1, 100, nameof(limit));
            if (err != null) return (err, null, null, null);
            limit = norm;
        }

        // ofs (0-1900)
        if (ofs.HasValue)
        {
            var (err, norm) = ValidateRange(ofs.Value, 0, 1900, nameof(ofs));
            if (err != null) return (err, null, null, null);
            ofs = norm;
        }

        // radius
        if (radius.HasValue)
        {
            var min = 1;
            var max = nearbyMode ? 50000 : 50000; // adhere to Nearby spec; others also accept radius
            var (err, norm) = ValidateRange(radius.Value, min, max, nameof(radius));
            if (err != null) return (err, null, null, null);
            radius = norm;
        }

        if (requireRadiusWithLatLon && !radius.HasValue)
        {
            // Not strictly required by all endpoints, but if intent is constrain area, radius is recommended.
            // We'll allow it and not error; comment left for clarity.
        }

        return (null, limit, ofs, radius);
    }

    private static string? ValidateCategorySet(string? categorySet)
    {
        if (string.IsNullOrWhiteSpace(categorySet)) return null;
        var parts = categorySet.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 10)
            return ResponseHelper.CreateValidationError("categorySet supports a maximum of 10 IDs.");
        foreach (var p in parts)
        {
            if (!int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return ResponseHelper.CreateValidationError("categorySet must be a comma-separated list of integer IDs.");
            }
        }
        return null;
    }

    private static string? ValidateCountrySet(string? countrySet)
    {
        if (string.IsNullOrWhiteSpace(countrySet)) return null;
        var parts = countrySet.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in parts)
        {
            if (p.Length != 2 || !p.All(char.IsLetter))
                return ResponseHelper.CreateValidationError("countrySet must be comma-separated 2-letter codes, e.g. 'US,CA'.");
        }
        return null;
    }

    private static string? ValidateOpeningHours(string? openingHours)
    {
        if (string.IsNullOrWhiteSpace(openingHours)) return null;
        if (!string.Equals(openingHours, "nextSevenDays", StringComparison.OrdinalIgnoreCase))
            return ResponseHelper.CreateValidationError("openingHours supports only 'nextSevenDays'.");
        return null;
    }

    private static string? ValidateFuzzyLevels(int? minLevel, int? maxLevel)
    {
        if (minLevel.HasValue && (minLevel.Value < 1 || minLevel.Value > 4))
            return ResponseHelper.CreateValidationError("minFuzzyLevel must be 1-4.");
        if (maxLevel.HasValue && (maxLevel.Value < 1 || maxLevel.Value > 4))
            return ResponseHelper.CreateValidationError("maxFuzzyLevel must be 1-4.");
        if (minLevel.HasValue && maxLevel.HasValue && minLevel.Value > maxLevel.Value)
            return ResponseHelper.CreateValidationError("minFuzzyLevel cannot be greater than maxFuzzyLevel.");
        return null;
    }

    private static readonly HashSet<string> ValidIdxTokens = new(StringComparer.OrdinalIgnoreCase)
    { "Addr", "Geo", "PAD", "POI", "Str", "Xstr" };

    private static string? ValidateIdxSet(string? idxSet)
    {
        if (string.IsNullOrWhiteSpace(idxSet)) return null;
        var parts = idxSet.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in parts)
        {
            if (!ValidIdxTokens.Contains(p))
                return ResponseHelper.CreateValidationError("idxSet values must be within {Addr, Geo, PAD, POI, Str, Xstr}.");
        }
        return null;
    }

    private static readonly HashSet<string> ValidEntityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Country", "CountrySubdivision", "CountrySecondarySubdivision", "CountryTertiarySubdivision",
        "Municipality", "MunicipalitySubdivision", "Neighbourhood", "PostalCodeArea"
    };

    private static string? ValidateBoundingBox(string? topLeft, string? btmRight)
    {
        if (string.IsNullOrWhiteSpace(topLeft) && string.IsNullOrWhiteSpace(btmRight)) return null;
        if (string.IsNullOrWhiteSpace(topLeft) || string.IsNullOrWhiteSpace(btmRight))
            return ResponseHelper.CreateValidationError("Both topLeft and btmRight must be provided together as 'lat,lon'.");
        if (!topLeft!.Contains(',') || !btmRight!.Contains(','))
            return ResponseHelper.CreateValidationError("Bounding box values must be in 'lat,lon' format.");
        return null;
    }

    private static string NormalizeCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return string.Empty;
        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(',', parts);
    }

    private static string NormalizeCountryCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return string.Empty;
        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToUpperInvariant());
        return string.Join(',', parts);
    }

    private static string? NormalizeEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static string? NormalizeEmpty(bool categorySet, string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

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
}
