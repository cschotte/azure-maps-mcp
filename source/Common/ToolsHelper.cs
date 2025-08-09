// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Core.GeoJson;
using Azure.Maps.Routing;

namespace Azure.Maps.Mcp.Common;

/// <summary>
/// Helper utilities shared across MCP Tools to avoid duplication
/// </summary>
public static class ToolsHelper
{
    // Shared routing option maps
    private static readonly Dictionary<string, TravelMode> s_travelModes = new(StringComparer.OrdinalIgnoreCase)
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

    private static readonly Dictionary<string, RouteType> s_routeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "fastest", RouteType.Fastest },
        { "shortest", RouteType.Shortest }
    };

    /// <summary>
    /// Parse and validate travel mode string
    /// </summary>
    public static (bool IsValid, string? Error, TravelMode Value) ParseTravelMode(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (false, "travelMode is required", default);

        if (s_travelModes.TryGetValue(input.Trim(), out var mode))
            return (true, null, mode);

        return (false, $"Invalid travel mode '{input}'. Valid options: {string.Join(", ", s_travelModes.Keys)}", default);
    }

    /// <summary>
    /// Parse and validate route type string
    /// </summary>
    public static (bool IsValid, string? Error, RouteType Value) ParseRouteType(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (false, "routeType is required", default);

        if (s_routeTypes.TryGetValue(input.Trim(), out var type))
            return (true, null, type);

        return (false, $"Invalid route type '{input}'. Valid options: {string.Join(", ", s_routeTypes.Keys)}", default);
    }

    /// <summary>
    /// Attempts to create a GeoPosition after validating coordinates
    /// </summary>
    public static bool TryCreateGeoPosition(double latitude, double longitude, out GeoPosition position, out string? error)
    {
        error = null;
        if (latitude < -90 || latitude > 90)
        {
            position = default;
            error = "Latitude must be between -90 and 90 degrees";
            return false;
        }

        if (longitude < -180 || longitude > 180)
        {
            position = default;
            error = "Longitude must be between -180 and 180 degrees";
            return false;
        }

        position = new GeoPosition(longitude, latitude);
        return true;
    }

    /// <summary>
    /// Try to parse a bounding box JSON string into GeoBoundingBox
    /// </summary>
    public static bool TryParseBoundingBox(string json, out GeoBoundingBox? bbox, out string? error)
    {
        bbox = null;
        error = null;
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, double>>(json);
            if (dict == null || !dict.TryGetValue("west", out var west) ||
                !dict.TryGetValue("south", out var south) ||
                !dict.TryGetValue("east", out var east) ||
                !dict.TryGetValue("north", out var north))
            {
                error = "Bounding box must contain 'west', 'south', 'east', and 'north' properties";
                return false;
            }

            bbox = new GeoBoundingBox(west, south, east, north);
            return true;
        }
        catch (JsonException)
        {
            error = "Invalid bounding box JSON format";
            return false;
        }
    }

    /// <summary>
    /// Calculate a normalized string similarity using Levenshtein distance
    /// </summary>
    public static double CalculateStringSimilarity(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2)) return 0;

        var longer = str1.Length > str2.Length ? str1 : str2;
        var shorter = str1.Length > str2.Length ? str2 : str1;
        if (longer.Length == 0) return 1.0;

        var editDistance = CalculateLevenshteinDistance(longer, shorter);
        return (longer.Length - editDistance) / (double)longer.Length;
    }

    private static int CalculateLevenshteinDistance(string s1, string s2)
    {
        var matrix = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++) matrix[i, 0] = i;
        for (int j = 0; j <= s2.Length; j++) matrix[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[s1.Length, s2.Length];
    }

    /// <summary>
    /// Convert common 3-letter country codes to 2-letter equivalents
    /// </summary>
    public static string? ConvertThreeLetterToTwoLetter(string threeLetterCode)
    {
        var conversionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["USA"] = "US", ["CAN"] = "CA", ["GBR"] = "GB", ["DEU"] = "DE", ["FRA"] = "FR",
            ["JPN"] = "JP", ["AUS"] = "AU", ["CHN"] = "CN", ["IND"] = "IN", ["BRA"] = "BR",
            ["RUS"] = "RU", ["ITA"] = "IT", ["ESP"] = "ES", ["MEX"] = "MX", ["KOR"] = "KR",
            ["NLD"] = "NL", ["BEL"] = "BE", ["CHE"] = "CH", ["AUT"] = "AT", ["SWE"] = "SE",
            ["NOR"] = "NO", ["DNK"] = "DK", ["FIN"] = "FI", ["POL"] = "PL", ["TUR"] = "TR"
        };

        return conversionMap.TryGetValue(threeLetterCode, out var twoLetterCode) ? twoLetterCode : null;
    }

    /// <summary>
    /// Generate alternative code variations for better matching
    /// </summary>
    public static List<string> GenerateCountryCodeVariations(string code)
    {
        var variations = new List<string>();

        if (string.IsNullOrWhiteSpace(code)) return variations;

        // Try lowercase and uppercase
        variations.Add(code.ToLowerInvariant());
        variations.Add(code.ToUpperInvariant());

        // Mixed case for 2-letter codes
        if (code.Length == 2)
        {
            variations.Add(char.ToUpper(code[0]) + code[1..].ToLower());
        }

        return variations.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
