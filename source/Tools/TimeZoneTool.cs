// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
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
    private readonly AtlasRestClient _atlas;

    public TimeZoneTool(
        IAzureMapsService mapsService,
        ILogger<TimeZoneTool> logger,
        AtlasRestClient atlas)
        : base(mapsService, logger)
    {
        _atlas = atlas;
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
            "includeTransitions",
            "string",
            "Include DST transitions: 'true' or 'false'. Default: false")]
        string includeTransitions = "false")
    {
        // Validate inputs using shared helpers
        var coordError = ValidateCoordinates(latitude, longitude);
        if (coordError != null)
        {
            return coordError;
        }

        var includeTransitionsOk = ValidationHelper.ValidateBooleanString(includeTransitions, nameof(includeTransitions));
        if (!includeTransitionsOk.IsValid) return ResponseHelper.CreateValidationError(includeTransitionsOk.ErrorMessage!);

        try
        {
            var query = new Dictionary<string, string?>
            {
                { "api-version", "1.0" },
                { "query", $"{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}" },
                { "options", includeTransitionsOk.Value ? "all" : "zoneInfo" }
            };

            var (ok, body, status, reason) = await _atlas.GetAsync("timezone/byCoordinates/json", query);

            if (!ok)
            {
                _logger.LogWarning("TimeZone API returned {Status}: {Body}", status, body);
                return ResponseHelper.CreateErrorResponse(
                    $"Timezone API error: {status} {reason}",
                    new { status, response = SafeParse(body) });
            }

            using var doc = JsonDocument.Parse(body);
            var summary = BuildSummaryFromResponse(doc.RootElement, latitude, longitude);

            return ResponseHelper.CreateSuccessResponse(new
            {
                query = new { latitude, longitude, includeTransitions = includeTransitionsOk.Value },
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

    // Removed advanced timestamp validation to keep the surface area minimal.
}
