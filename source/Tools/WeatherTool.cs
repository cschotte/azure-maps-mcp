// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Maps.Mcp.Common;
using Azure.Maps.Mcp.Services;

namespace Azure.Maps.Mcp.Tools;

/// <summary>
/// Weather tool using Azure Maps Weather REST APIs.
/// Pragmatic set: current conditions, hourly forecast, daily forecast, severe alerts.
/// </summary>
public sealed class WeatherTool : BaseMapsTool
{
    private readonly AtlasRestClient _atlas;

    public WeatherTool(
        IAzureMapsService mapsService,
        ILogger<WeatherTool> logger,
        AtlasRestClient atlas)
        : base(mapsService, logger)
    {
        _atlas = atlas;
    }

    [Function(nameof(GetCurrentConditions))]
    public async Task<string> GetCurrentConditions(
        [McpToolTrigger(
            "weather_current",
            "Get current weather conditions for coordinates (temp, precip, wind, humidity, etc.).")]
        ToolInvocationContext context,
        [McpToolProperty("latitude", "number", "Latitude (-90 to 90)")] double latitude,
        [McpToolProperty("longitude", "number", "Longitude (-180 to 180)")] double longitude,
        [McpToolProperty("unit", "string", "Units: metric | imperial. Default: metric")] string unit = "metric",
        [McpToolProperty("duration", "number", "Past hours to include: 0 | 6 | 24. Default: 0")] int duration = 0,
        [McpToolProperty("language", "string", "Optional IETF language tag, e.g. en-US")] string? language = null
    )
    {
        var coordError = ValidateCoordinates(latitude, longitude);
        if (coordError != null) return coordError;

        if (!IsValidUnit(unit))
            return ResponseHelper.CreateValidationError("unit must be 'metric' or 'imperial'");
        if (duration is not (0 or 6 or 24))
            return ResponseHelper.CreateValidationError("duration must be 0, 6, or 24");

        try
        {
            var (ok, body, status, reason) = await _atlas.GetAsync(
                path: "weather/currentConditions/json",
                query: new Dictionary<string, string?>
                {
                    { "api-version", "1.1" },
                    { "query", $"{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}" },
                    { "unit", unit },
                    { "duration", duration.ToString() },
                    { "language", string.IsNullOrWhiteSpace(language) ? null : language }
                });
            if (!ok) return ResponseHelper.CreateErrorResponse($"Weather API error: {status} {reason}", new { status, response = SafeParse(body) });

            using var doc = JsonDocument.Parse(body);
            var summary = SimplifyCurrent(doc.RootElement);
            return ResponseHelper.CreateSuccessResponse(new { query = new { latitude, longitude, unit, duration, language }, summary, raw = doc.RootElement });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current conditions");
            return ResponseHelper.CreateErrorResponse("Failed to get current conditions");
        }
    }

    [Function(nameof(GetHourlyForecast))]
    public async Task<string> GetHourlyForecast(
        [McpToolTrigger(
            "weather_hourly",
            "Get hourly forecast for coordinates for 1, 12, 24, 72, 120, or 240 hours.")]
        ToolInvocationContext context,
        [McpToolProperty("latitude", "number", "Latitude (-90 to 90)")] double latitude,
        [McpToolProperty("longitude", "number", "Longitude (-180 to 180)")] double longitude,
        [McpToolProperty("duration", "number", "Hours: 1|12|24|72|120|240 (subject to SKU). Default: 24")] int duration = 24,
        [McpToolProperty("unit", "string", "Units: metric | imperial. Default: metric")] string unit = "metric",
        [McpToolProperty("language", "string", "Optional IETF language tag, e.g. en-US")] string? language = null
    )
    {
        var coordError = ValidateCoordinates(latitude, longitude);
        if (coordError != null) return coordError;

        if (!IsValidUnit(unit))
            return ResponseHelper.CreateValidationError("unit must be 'metric' or 'imperial'");
        var validHours = new HashSet<int> { 1, 12, 24, 72, 120, 240 };
        if (!validHours.Contains(duration))
            return ResponseHelper.CreateValidationError("duration must be one of 1,12,24,72,120,240");

        try
        {
            var (ok, body, status, reason) = await _atlas.GetAsync(
                path: "weather/forecast/hourly/json",
                query: new Dictionary<string, string?>
                {
                    { "api-version", "1.1" },
                    { "query", $"{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}" },
                    { "unit", unit },
                    { "duration", duration.ToString() },
                    { "language", string.IsNullOrWhiteSpace(language) ? null : language }
                });
            if (!ok) return ResponseHelper.CreateErrorResponse($"Weather API error: {status} {reason}", new { status, response = SafeParse(body) });

            using var doc = JsonDocument.Parse(body);
            var summary = SimplifyHourly(doc.RootElement);
            return ResponseHelper.CreateSuccessResponse(new { query = new { latitude, longitude, unit, duration, language }, summary, raw = doc.RootElement });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get hourly forecast");
            return ResponseHelper.CreateErrorResponse("Failed to get hourly forecast");
        }
    }

    [Function(nameof(GetDailyForecast))]
    public async Task<string> GetDailyForecast(
        [McpToolTrigger(
            "weather_daily",
            "Get daily forecast for coordinates for 1,5,10,25,45 days (subject to SKU).")]
        ToolInvocationContext context,
        [McpToolProperty("latitude", "number", "Latitude (-90 to 90)")] double latitude,
        [McpToolProperty("longitude", "number", "Longitude (-180 to 180)")] double longitude,
        [McpToolProperty("duration", "number", "Days: 1|5|10|25|45. Default: 5")] int duration = 5,
        [McpToolProperty("unit", "string", "Units: metric | imperial. Default: metric")] string unit = "metric",
        [McpToolProperty("language", "string", "Optional IETF language tag, e.g. en-US")] string? language = null
    )
    {
        var coordError = ValidateCoordinates(latitude, longitude);
        if (coordError != null) return coordError;

        if (!IsValidUnit(unit))
            return ResponseHelper.CreateValidationError("unit must be 'metric' or 'imperial'");
        var validDays = new HashSet<int> { 1, 5, 10, 25, 45 };
        if (!validDays.Contains(duration))
            return ResponseHelper.CreateValidationError("duration must be one of 1,5,10,25,45");

        try
        {
            var (ok, body, status, reason) = await _atlas.GetAsync(
                path: "weather/forecast/daily/json",
                query: new Dictionary<string, string?>
                {
                    { "api-version", "1.1" },
                    { "query", $"{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}" },
                    { "unit", unit },
                    { "duration", duration.ToString() },
                    { "language", string.IsNullOrWhiteSpace(language) ? null : language }
                });
            if (!ok) return ResponseHelper.CreateErrorResponse($"Weather API error: {status} {reason}", new { status, response = SafeParse(body) });

            using var doc = JsonDocument.Parse(body);
            var summary = SimplifyDaily(doc.RootElement);
            return ResponseHelper.CreateSuccessResponse(new { query = new { latitude, longitude, unit, duration, language }, summary, raw = doc.RootElement });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get daily forecast");
            return ResponseHelper.CreateErrorResponse("Failed to get daily forecast");
        }
    }

    [Function(nameof(GetSevereAlerts))]
    public async Task<string> GetSevereAlerts(
        [McpToolTrigger(
            "weather_alerts",
            "Get severe weather alerts near coordinates (hurricanes, storms, flooding, heat, etc.).")]
        ToolInvocationContext context,
        [McpToolProperty("latitude", "number", "Latitude (-90 to 90)")] double latitude,
        [McpToolProperty("longitude", "number", "Longitude (-180 to 180)")] double longitude,
        [McpToolProperty("language", "string", "Optional IETF language tag, e.g. en-US")] string? language = null,
        [McpToolProperty("details", "string", "Return full area-specific details: true|false. Default: true")] string details = "true"
    )
    {
        var coordError = ValidateCoordinates(latitude, longitude);
        if (coordError != null) return coordError;

        var detailsOk = ValidationHelper.ValidateBooleanString(details, "details");
        if (!detailsOk.IsValid) return ResponseHelper.CreateValidationError(detailsOk.ErrorMessage!);

        try
        {
            var (ok, body, status, reason) = await _atlas.GetAsync(
                path: "weather/severe/alerts/json",
                query: new Dictionary<string, string?>
                {
                    { "api-version", "1.1" },
                    { "query", $"{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}" },
                    { "details", detailsOk.Value ? "true" : "false" },
                    { "language", string.IsNullOrWhiteSpace(language) ? null : language }
                });
            if (!ok) return ResponseHelper.CreateErrorResponse($"Weather API error: {status} {reason}", new { status, response = SafeParse(body) });

            using var doc = JsonDocument.Parse(body);
            var summary = SimplifyAlerts(doc.RootElement);
            return ResponseHelper.CreateSuccessResponse(new { query = new { latitude, longitude, language, details = detailsOk.Value }, summary, raw = doc.RootElement });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get severe alerts");
            return ResponseHelper.CreateErrorResponse("Failed to get severe alerts");
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

    private static bool IsValidUnit(string unit)
        => string.Equals(unit, "metric", StringComparison.OrdinalIgnoreCase) || string.Equals(unit, "imperial", StringComparison.OrdinalIgnoreCase);

    private static object SimplifyCurrent(JsonElement root)
    {
        try
        {
            if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
                return new { count = 0 };

            var r = results[0];
            return new
            {
                observed = r.TryGetProperty("dateTime", out var dt) ? dt.GetString() : null,
                phrase = r.TryGetProperty("phrase", out var ph) ? ph.GetString() : null,
                iconCode = r.TryGetProperty("iconCode", out var ic) ? ic.GetInt32() : (int?)null,
                isDayTime = r.TryGetProperty("isDayTime", out var idt) && idt.GetBoolean(),
                temperature = ExtractUnit(r, "temperature"),
                humidity = r.TryGetProperty("relativeHumidity", out var rh) ? rh.GetInt32() : (int?)null,
                wind = ExtractWind(r, "wind"),
                gust = ExtractWind(r, "windGust"),
                precipitation = r.TryGetProperty("hasPrecipitation", out var hp) && hp.GetBoolean(),
                precip1h = ExtractUnit(r.GetProperty("precipitationSummary"), "pastHour")
            };
        }
        catch
        {
            return new { count = (int?)null };
        }
    }

    private static object SimplifyHourly(JsonElement root)
    {
        try
        {
            if (!root.TryGetProperty("forecasts", out var forecasts) || forecasts.ValueKind != JsonValueKind.Array)
                return new { count = 0, items = Array.Empty<object>() };

            var items = new List<object>();
            foreach (var f in forecasts.EnumerateArray())
            {
                items.Add(new
                {
                    time = f.TryGetProperty("date", out var dt) ? dt.GetString() : null,
                    phrase = f.TryGetProperty("iconPhrase", out var ip) ? ip.GetString() : null,
                    iconCode = f.TryGetProperty("iconCode", out var ic) ? ic.GetInt32() : (int?)null,
                    isDaylight = f.TryGetProperty("isDaylight", out var dl) && dl.GetBoolean(),
                    temperature = ExtractUnit(f, "temperature"),
                    precipProb = f.TryGetProperty("precipitationProbability", out var pp) ? pp.GetInt32() : (int?)null,
                    rainProb = f.TryGetProperty("rainProbability", out var rp) ? rp.GetInt32() : (int?)null,
                    snowProb = f.TryGetProperty("snowProbability", out var sp) ? sp.GetInt32() : (int?)null,
                    wind = ExtractWind(f, "wind"),
                    gust = ExtractWind(f, "windGust")
                });
            }
            return new { count = items.Count, items };
        }
        catch
        {
            return new { count = (int?)null };
        }
    }

    private static object SimplifyDaily(JsonElement root)
    {
        try
        {
            if (!root.TryGetProperty("forecasts", out var forecasts) || forecasts.ValueKind != JsonValueKind.Array)
                return new { count = 0, items = Array.Empty<object>() };

            var items = new List<object>();
            foreach (var f in forecasts.EnumerateArray())
            {
                object? min = null, max = null;
                if (f.TryGetProperty("temperature", out var t))
                {
                    min = ExtractUnit(t, "minimum");
                    max = ExtractUnit(t, "maximum");
                }
                items.Add(new
                {
                    date = f.TryGetProperty("date", out var dt) ? dt.GetString() : null,
                    temp = new { min, max },
                    sunHours = f.TryGetProperty("hoursOfSun", out var hs) ? hs.GetDouble() : (double?)null,
                    day = SimplifyDayNight(f, "day"),
                    night = SimplifyDayNight(f, "night")
                });
            }
            return new { count = items.Count, items };
        }
        catch
        {
            return new { count = (int?)null };
        }
    }

    private static object SimplifyDayNight(JsonElement f, string prop)
    {
        if (!f.TryGetProperty(prop, out var dn) || dn.ValueKind != JsonValueKind.Object)
            return new { };
        return new
        {
            phrase = dn.TryGetProperty("iconPhrase", out var ip) ? ip.GetString() : null,
            iconCode = dn.TryGetProperty("iconCode", out var ic) ? ic.GetInt32() : (int?)null,
            precip = dn.TryGetProperty("hasPrecipitation", out var hp) && hp.GetBoolean(),
            precipType = dn.TryGetProperty("precipitationType", out var pt) ? pt.GetString() : null,
            precipProb = dn.TryGetProperty("precipitationProbability", out var pp) ? pp.GetInt32() : (int?)null,
            rain = ExtractUnit(dn, "rain"),
            snow = ExtractUnit(dn, "snow"),
            total = ExtractUnit(dn, "totalLiquid"),
            wind = ExtractWind(dn, "wind"),
            gust = ExtractWind(dn, "windGust")
        };
    }

    private static object SimplifyAlerts(JsonElement root)
    {
        try
        {
            if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                return new { count = 0, items = Array.Empty<object>() };

            var items = new List<object>();
            foreach (var r in results.EnumerateArray())
            {
                items.Add(new
                {
                    id = r.TryGetProperty("alertId", out var id) ? id.GetInt32() : (int?)null,
                    country = r.TryGetProperty("countryCode", out var cc) ? cc.GetString() : null,
                    category = r.TryGetProperty("category", out var cat) ? cat.GetString() : null,
                    source = r.TryGetProperty("source", out var src) ? src.GetString() : null,
                    description = r.TryGetProperty("description", out var desc) && desc.TryGetProperty("localized", out var loc) ? loc.GetString() : null,
                    areas = ExtractAlertAreas(r)
                });
            }
            return new { count = items.Count, items };
        }
        catch
        {
            return new { count = (int?)null };
        }
    }

    private static List<object> ExtractAlertAreas(JsonElement r)
    {
        var list = new List<object>();
        if (!r.TryGetProperty("alertAreas", out var areas) || areas.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var a in areas.EnumerateArray())
        {
            list.Add(new
            {
                name = a.TryGetProperty("name", out var n) ? n.GetString() : null,
                summary = a.TryGetProperty("summary", out var s) ? s.GetString() : null,
                start = a.TryGetProperty("startTime", out var st) ? st.GetString() : null,
                end = a.TryGetProperty("endTime", out var et) ? et.GetString() : null
            });
        }
        return list;
    }

    private static object? ExtractUnit(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var u) || u.ValueKind != JsonValueKind.Object)
            return null;
        return new
        {
            value = u.TryGetProperty("value", out var v) ? v.GetDouble() : (double?)null,
            unit = u.TryGetProperty("unit", out var un) ? un.GetString() : null
        };
    }

    private static object? ExtractWind(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var w) || w.ValueKind != JsonValueKind.Object)
            return null;
        object? dir = null;
        if (w.TryGetProperty("direction", out var d))
        {
            dir = new
            {
                degrees = d.TryGetProperty("degrees", out var deg) ? deg.GetInt32() : (int?)null,
                text = d.TryGetProperty("localizedDescription", out var ld) ? ld.GetString() : null
            };
        }
        return new { direction = dir, speed = ExtractUnit(w, "speed") };
    }
}
