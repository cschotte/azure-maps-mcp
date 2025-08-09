// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace Azure.Maps.Mcp.Common;

/// <summary>
/// Simple response builder utilities for Azure Maps MCP
/// </summary>
public static class ResponseHelper
{
    /// <summary>
    /// Creates a success response with data
    /// </summary>
    public static string CreateSuccessResponse(object data)
    {
        return JsonSerializer.Serialize(new { success = true, data }, new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// Creates an error response
    /// </summary>
    public static string CreateErrorResponse(string error, object? context = null)
    {
        var response = new { error, context };
        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// Creates a batch response summary
    /// </summary>
    public static object CreateBatchSummary<T>(List<T> successResults, List<object> failedResults, int originalCount)
    {
        return new
        {
            Summary = new
            {
                Total = originalCount,
                Successful = successResults.Count,
                Failed = failedResults.Count,
                SuccessRate = originalCount > 0 ? Math.Round((double)successResults.Count / originalCount * 100, 1) : 0
            },
            Results = new
            {
                Successful = successResults,
                Failed = failedResults.Take(5) // Limit failed results to avoid overflow
            }
        };
    }

    /// <summary>
    /// Creates location information
    /// </summary>
    public static object CreateLocationSummary(double latitude, double longitude, object? addressInfo = null)
    {
        return new
        {
            Coordinates = new { Latitude = latitude, Longitude = longitude },
            Address = addressInfo
        };
    }

    /// <summary>
    /// Creates country information
    /// </summary>
    public static object CreateCountryInfo(string countryCode, string countryName, string? continent = null)
    {
        var result = new Dictionary<string, object>
        {
            ["CountryCode"] = countryCode,
            ["CountryName"] = countryName
        };

        if (!string.IsNullOrEmpty(continent))
            result["Continent"] = continent;

        return result;
    }

    /// <summary>
    /// Creates a validation error response
    /// </summary>
    public static string CreateValidationError(string error, List<string>? suggestions = null)
    {
        var response = new Dictionary<string, object> { ["error"] = error };
        
        if (suggestions?.Any() == true)
            response["suggestions"] = suggestions.Take(3).ToList();

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// Determines the precision level based on coordinate values
    /// </summary>
    public static string DeterminePrecisionLevel(double latitude, double longitude)
    {
        // Count decimal places to determine precision
        var latDecimalPlaces = CountDecimalPlaces(latitude);
        var lonDecimalPlaces = CountDecimalPlaces(longitude);
        var maxDecimalPlaces = Math.Max(latDecimalPlaces, lonDecimalPlaces);

        return maxDecimalPlaces switch
        {
            >= 6 => "very high (meter-level)",
            >= 4 => "high (10-meter level)",
            >= 3 => "medium (100-meter level)",
            >= 2 => "low (kilometer level)",
            _ => "very low (10+ kilometer level)"
        };
    }

    private static int CountDecimalPlaces(double value)
    {
        var str = value.ToString("F15").TrimEnd('0');
        var decimalIndex = str.IndexOf('.');
        return decimalIndex >= 0 ? str.Length - decimalIndex - 1 : 0;
    }
}
