// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace Azure.Maps.Mcp.Common;

/// <summary>
/// Common response builder utilities optimized for AI/LLM consumption
/// </summary>
public static class ResponseHelper
{
    /// <summary>
    /// Creates a simple success response without timing information
    /// </summary>
    public static string CreateSuccessResponse(object data)
    {
        return JsonSerializer.Serialize(new { success = true, data }, new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// Creates a simple error response
    /// </summary>
    public static string CreateErrorResponse(string error, object? context = null)
    {
        var response = new { error, context };
        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// Creates a batch response optimized for AI consumption
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
                Failed = failedResults.Take(5) // Limit failed results to avoid token overflow
            }
        };
    }

    /// <summary>
    /// Extracts essential location information for AI consumption
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
    /// Creates a simplified country information response
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
    /// Creates a simple validation error response with suggestions
    /// </summary>
    public static string CreateValidationError(string error, List<string>? suggestions = null)
    {
        var response = new Dictionary<string, object> { ["error"] = error };
        
        if (suggestions?.Any() == true)
            response["suggestions"] = suggestions.Take(3).ToList(); // Limit suggestions

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = false });
    }
}
