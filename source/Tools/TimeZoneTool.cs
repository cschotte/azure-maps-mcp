// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Maps.Mcp.Common;
using Azure.Maps.Mcp.Services;

namespace Azure.Maps.Mcp.Tools;

/// <summary>
/// TimeZone tool using Azure Maps REST API (no SDK available for this API)
/// </summary>
public sealed class TimeZoneTool : BaseMapsTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _subscriptionKey;

    public TimeZoneTool(
        IAzureMapsService mapsService,
        ILogger<TimeZoneTool> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
        : base(mapsService, logger)
    {
        _httpClientFactory = httpClientFactory;
        _subscriptionKey =
            configuration["AZURE_MAPS_SUBSCRIPTION_KEY"] ??
            configuration["Values:AZURE_MAPS_SUBSCRIPTION_KEY"] ??
            throw new InvalidOperationException("AZURE_MAPS_SUBSCRIPTION_KEY is required for TimeZone API calls");
    }

    /// <summary>
    /// Get time zone information for a latitude/longitude using Azure Maps REST API.
    /// </summary>
    [Function(nameof(GetTimeZoneByCoordinates))]
    public async Task<string> GetTimeZoneByCoordinates(
        [McpToolTrigger(
            "timezone_by_coordinates",
            "Get current and future time zone info (offset, DST, names, sunrise/sunset) for coordinates.")]
        ToolInvocationContext context,
        [McpToolProperty("latitude", "number", "Latitude (-90 to 90). Example: 47.6062")] double latitude,
        [McpToolProperty("longitude", "number", "Longitude (-180 to 180). Example: -122.3321")] double longitude,
        [McpToolProperty(
            "options",
            "string",
            "Optional detail level: none | zoneInfo | transitions | all. Default: zoneInfo")]
        string? options = "zoneInfo",
        [McpToolProperty(
            "timeStamp",
            "string",
            "Optional ISO 8601 timestamp to evaluate (e.g. 2025-08-10T12:00:00Z). Default: now on server")]
        string? timeStamp = null,
        [McpToolProperty(
            "transitionsFrom",
            "string",
            "Optional ISO 8601 start date for DST transitions (only when options=transitions or all)")]
        string? transitionsFrom = null,
        [McpToolProperty(
            "transitionsYears",
            "number",
            "Optional number of years from transitionsFrom for DST transitions (1-5 typical)")]
        int? transitionsYears = null)
    {
        // Validate inputs using shared helpers
        var coordError = ValidateCoordinates(latitude, longitude);
        if (coordError != null)
        {
            return coordError;
        }

        // Normalize and validate options
        var validOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "none", "zoneInfo", "transitions", "all" };
        var selectedOptions = string.IsNullOrWhiteSpace(options) ? "zoneInfo" : options.Trim();
        if (!validOptions.Contains(selectedOptions))
        {
            return ResponseHelper.CreateValidationError(
                $"Invalid options '{options}'. Valid: none | zoneInfo | transitions | all");
        }

        // Validate timestamps if provided
        if (!IsNullOrIso8601(timeStamp))
        {
            return ResponseHelper.CreateValidationError(
                "timeStamp must be ISO 8601 (e.g., 2025-08-10T12:00:00Z)");
        }
        if (!IsNullOrIso8601(transitionsFrom))
        {
            return ResponseHelper.CreateValidationError(
                "transitionsFrom must be ISO 8601 date/time");
        }

        if (transitionsYears is < 0 or > 10)
        {
            return ResponseHelper.CreateValidationError(
                "transitionsYears must be between 0 and 10 if provided");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://atlas.microsoft.com/");

            var q = new List<string>
            {
                $"api-version=1.0",
                $"subscription-key={WebUtility.UrlEncode(_subscriptionKey)}",
                // Ensure invariant culture for decimals
                $"query={latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}"
            };

            if (!string.Equals(selectedOptions, "zoneInfo", StringComparison.OrdinalIgnoreCase))
            {
                q.Add($"options={WebUtility.UrlEncode(selectedOptions)}");
            }
            if (!string.IsNullOrWhiteSpace(timeStamp)) q.Add($"timeStamp={WebUtility.UrlEncode(timeStamp)}");
            if (!string.IsNullOrWhiteSpace(transitionsFrom)) q.Add($"transitionsFrom={WebUtility.UrlEncode(transitionsFrom)}");
            if (transitionsYears.HasValue) q.Add($"transitionsYears={transitionsYears.Value}");

            var url = $"timezone/byCoordinates/json?{string.Join('&', q)}";

            _logger.LogInformation(
                "Requesting TimeZone by coordinates: lat={Lat}, lon={Lon}, options={Options}",
                latitude, longitude, selectedOptions);

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Accept", "application/json");

            using var resp = await client.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("TimeZone API returned {Status}: {Body}", (int)resp.StatusCode, body);
                return ResponseHelper.CreateErrorResponse(
                    $"Timezone API error: {(int)resp.StatusCode} {resp.ReasonPhrase}",
                    new { status = (int)resp.StatusCode, response = SafeParse(body) });
            }

            using var doc = JsonDocument.Parse(body);
            var summary = BuildSummaryFromResponse(doc.RootElement, latitude, longitude);

            return ResponseHelper.CreateSuccessResponse(new
            {
                query = new { latitude, longitude, options = selectedOptions },
                summary,
                raw = doc.RootElement
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch timezone by coordinates");
            return ResponseHelper.CreateErrorResponse("Failed to fetch timezone information");
        }
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

    private static object BuildSummaryFromResponse(JsonElement root, double lat, double lon)
    {
        try
        {
            var tzArray = root.GetProperty("TimeZones");
            if (tzArray.ValueKind != JsonValueKind.Array || tzArray.GetArrayLength() == 0)
            {
                return new { found = 0 };
            }

            var tz0 = tzArray[0];
            string id = tz0.TryGetProperty("Id", out var idEl) ? idEl.GetString() ?? "" : "";
            string? standardOffset = tz0.GetProperty("ReferenceTime").TryGetProperty("StandardOffset", out var so) ? so.GetString() : null;
            string? daylight = tz0.GetProperty("ReferenceTime").TryGetProperty("DaylightSavings", out var ds) ? ds.GetString() : null;
            string? tag = tz0.GetProperty("ReferenceTime").TryGetProperty("Tag", out var tagEl) ? tagEl.GetString() : null;
            string? wall = tz0.GetProperty("ReferenceTime").TryGetProperty("WallTime", out var wt) ? wt.GetString() : null;
            string? sunrise = tz0.GetProperty("ReferenceTime").TryGetProperty("Sunrise", out var sr) ? sr.GetString() : null;
            string? sunset = tz0.GetProperty("ReferenceTime").TryGetProperty("Sunset", out var ss) ? ss.GetString() : null;

            var std = ParseOffset(standardOffset);
            var dst = ParseOffset(daylight);
            var total = std + dst;

            return new
            {
                found = tzArray.GetArrayLength(),
                timeZoneId = id,
                name = tz0.TryGetProperty("Names", out var names) && names.TryGetProperty("Generic", out var gen)
                    ? gen.GetString()
                    : null,
                tag,
                offset = new
                {
                    standard = FormatOffset(std),
                    daylight = FormatOffset(dst),
                    total = FormatOffset(total)
                },
                wallTime = wall,
                sunrise,
                sunset,
                coordinates = new { latitude = lat, longitude = lon }
            };
        }
        catch
        {
            return new { found = (int?)null };
        }
    }

    private static TimeSpan ParseOffset(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return TimeSpan.Zero;

        // Azure Maps returns format like "+01:00:00" or "-08:00:00"
        if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var ts))
            return ts;
        // Fallback: try HH:mm
        if (TimeSpan.TryParseExact(s, new[] { "hh\\:mm", "hh\\:mm\\:ss" }, CultureInfo.InvariantCulture, out ts))
            return ts;
        return TimeSpan.Zero;
    }

    private static string FormatOffset(TimeSpan ts)
    {
        var sign = ts < TimeSpan.Zero ? "-" : "+";
        var abs = ts.Duration();
        return $"{sign}{abs:hh\\:mm}";
    }

    private static bool IsNullOrIso8601(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _);
    }
}
