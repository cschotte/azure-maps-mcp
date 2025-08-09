// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Azure.Maps.Mcp.Services;
using Azure.Maps.Mcp.Common;

namespace Azure.Maps.Mcp.Tools;

/// <summary>
/// Base class for Azure Maps MCP tools providing common functionality
/// </summary>
public abstract class BaseMapsTool
{
    protected readonly IAzureMapsService _mapsService;
    protected readonly ILogger _logger;

    protected BaseMapsTool(IAzureMapsService mapsService, ILogger logger)
    {
        _mapsService = mapsService;
        _logger = logger;
    }

    /// <summary>
    /// Common error handling wrapper for tool operations
    /// </summary>
    protected async Task<string> ExecuteWithErrorHandling(
        Func<Task<object>> operation,
        string operationName,
        object? context = null)
    {
        try
        {
            var result = await operation();
            return ResponseHelper.CreateSuccessResponse(result);
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Maps API error during {Operation}: {Message}", operationName, ex.Message);
            return ResponseHelper.CreateErrorResponse($"API Error: {ex.Message}", context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during {Operation}", operationName);
            return ResponseHelper.CreateErrorResponse("An unexpected error occurred", context);
        }
    }

    /// <summary>
    /// Validates coordinates and returns error response if invalid
    /// </summary>
    protected string? ValidateCoordinates(double latitude, double longitude)
    {
        var validation = ValidationHelper.ValidateCoordinates(latitude, longitude);
        return validation.IsValid ? null : ResponseHelper.CreateErrorResponse(validation.ErrorMessage!);
    }

    /// <summary>
    /// Validates string input and returns error response if invalid
    /// </summary>
    protected string? ValidateStringInput(string? value, int minLength = 1, int maxLength = 2048, string fieldName = "Input")
    {
        var validation = ValidationHelper.ValidateStringInput(value, minLength, maxLength, fieldName);
        return validation.IsValid ? null : ResponseHelper.CreateErrorResponse(validation.ErrorMessage!);
    }

    /// <summary>
    /// Validates range and returns normalized value or error response
    /// </summary>
    protected (string? error, int normalizedValue) ValidateRange(int value, int min, int max, string fieldName = "Value")
    {
        var validation = ValidationHelper.ValidateRange(value, min, max, fieldName);
        return validation.IsValid 
            ? (null, validation.NormalizedValue)
            : (ResponseHelper.CreateErrorResponse(validation.ErrorMessage!), validation.NormalizedValue);
    }
}
