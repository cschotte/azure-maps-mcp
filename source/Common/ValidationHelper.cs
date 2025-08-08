// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text.RegularExpressions;

namespace Azure.Maps.Mcp.Common;

/// <summary>
/// Common validation utilities for Azure Maps MCP tools
/// </summary>
public static class ValidationHelper
{
    /// <summary>
    /// Validates and normalizes input parameters commonly used across tools
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="minLength">Minimum length required</param>
    /// <param name="maxLength">Maximum length allowed</param>
    /// <param name="fieldName">Name of the field for error messages</param>
    /// <returns>Validation result with error message if invalid</returns>
    public static (bool IsValid, string? ErrorMessage) ValidateStringInput(
        string? value, 
        int minLength = 1, 
        int maxLength = 2048, 
        string fieldName = "Input")
    {
        if (string.IsNullOrWhiteSpace(value))
            return (false, $"{fieldName} is required");

        var trimmed = value.Trim();
        if (trimmed.Length < minLength)
            return (false, $"{fieldName} must be at least {minLength} characters long");

        if (trimmed.Length > maxLength)
            return (false, $"{fieldName} exceeds maximum length of {maxLength} characters");

        return (true, null);
    }

    /// <summary>
    /// Validates and normalizes a range value (like maxResults)
    /// </summary>
    public static (bool IsValid, string? ErrorMessage, int NormalizedValue) ValidateRange(
        int value, 
        int min, 
        int max, 
        string fieldName = "Value")
    {
        if (value < min || value > max)
            return (false, $"{fieldName} must be between {min} and {max}", Math.Max(min, Math.Min(max, value)));

        return (true, null, value);
    }

    /// <summary>
    /// Validates IP address format
    /// </summary>
    public static (bool IsValid, string? ErrorMessage, IPAddress? ParsedIP) ValidateIPAddress(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return (false, "IP address is required", null);

        if (!IPAddress.TryParse(ipAddress.Trim(), out var parsedIP))
            return (false, $"Invalid IP address format: '{ipAddress}'", null);

        return (true, null, parsedIP);
    }

    /// <summary>
    /// Validates coordinate values
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateCoordinates(double latitude, double longitude)
    {
        if (latitude < -90 || latitude > 90)
            return (false, "Latitude must be between -90 and 90 degrees");

        if (longitude < -180 || longitude > 180)
            return (false, "Longitude must be between -180 and 180 degrees");

        return (true, null);
    }

    /// <summary>
    /// Validates boolean string input
    /// </summary>
    public static (bool IsValid, string? ErrorMessage, bool Value) ValidateBooleanString(string? input, string fieldName = "Value")
    {
        if (string.IsNullOrWhiteSpace(input))
            return (false, $"{fieldName} is required", false);

        if (bool.TryParse(input.Trim(), out var result))
            return (true, null, result);

        return (false, $"Invalid {fieldName}. Use 'true' or 'false'", false);
    }

    /// <summary>
    /// Validates enum value from string
    /// </summary>
    public static (bool IsValid, string? ErrorMessage, T Value) ValidateEnum<T>(
        string? input, 
        Dictionary<string, T> validValues, 
        string fieldName = "Value") where T : struct
    {
        if (string.IsNullOrWhiteSpace(input))
            return (false, $"{fieldName} is required", default(T));

        if (validValues.TryGetValue(input.Trim(), out var value))
            return (true, null, value);

        var validOptions = string.Join(", ", validValues.Keys);
        return (false, $"Invalid {fieldName} '{input}'. Valid options: {validOptions}", default(T));
    }

    /// <summary>
    /// Validates and removes duplicates from array input
    /// </summary>
    public static (bool IsValid, string? ErrorMessage, HashSet<string>? UniqueValues) ValidateArrayInput(
        string[]? values, 
        int maxCount = 100, 
        string fieldName = "Array")
    {
        if (values == null || values.Length == 0)
            return (false, $"At least one {fieldName.ToLower()} is required", null);

        if (values.Length > maxCount)
            return (false, $"Maximum {maxCount} {fieldName.ToLower()}s allowed", null);

        var unique = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (unique.Count == 0)
            return (false, $"No valid {fieldName.ToLower()}s provided", null);

        return (true, null, unique);
    }

    /// <summary>
    /// Helper method to determine if an IP address is private
    /// </summary>
    public static bool IsPrivateIP(IPAddress ip)
    {
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            // 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 127.0.0.0/8
            return bytes[0] == 10 ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168) ||
                   bytes[0] == 127;
        }
        
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || IPAddress.IsLoopback(ip);
        }
        
        return false;
    }

    /// <summary>
    /// Common coordinate validation for all tools
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateCoordinateArray<T>(T[] coordinates, Func<T, (double Latitude, double Longitude)> extractCoords, int minCount = 2)
        where T : class
    {
        if (coordinates == null || coordinates.Length < minCount)
            return (false, $"At least {minCount} coordinates are required");

        for (int i = 0; i < coordinates.Length; i++)
        {
            var (lat, lon) = extractCoords(coordinates[i]);
            var validation = ValidateCoordinates(lat, lon);
            if (!validation.IsValid)
                return (false, $"Coordinate {i + 1}: {validation.ErrorMessage}");
        }

        return (true, null);
    }
}
