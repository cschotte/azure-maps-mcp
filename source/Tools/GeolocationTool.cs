// Copyright (c) 2025 Clemens Schotte
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using Azure.Maps.Mcp.Services;
using Azure.Maps.Geolocation;
using System.Net;
using System.Text.Json;

namespace Azure.Maps.Mcp.Tools;

/// <summary>
/// Azure Maps Geolocation Tool providing IP-based geolocation and country code lookup capabilities
/// </summary>
public class GeolocationTool(IAzureMapsService azureMapsService, ILogger<GeolocationTool> logger)
{
    private readonly MapsGeolocationClient _geolocationClient = azureMapsService.GeolocationClient;

    /// <summary>
    /// Get country code and location information for an IP address
    /// </summary>
    [Function(nameof(GetCountryCodeByIP))]
    public async Task<string> GetCountryCodeByIP(
        [McpToolTrigger(
            "get_country_code_by_ip",
            "Get country code and location information for a given IP address. Supports both IPv4 and IPv6 addresses."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "ipAddress",
            "string",
            "The IP address to look up (IPv4 or IPv6). Examples: '8.8.8.8', '2001:4898:80e8:b::189'"
        )] string ipAddress
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return JsonSerializer.Serialize(new { error = "IP address is required" });
            }

            if (!IPAddress.TryParse(ipAddress, out var parsedIP))
            {
                return JsonSerializer.Serialize(new { error = $"Invalid IP address format: '{ipAddress}'. Please provide a valid IPv4 or IPv6 address." });
            }

            logger.LogInformation("Getting country code for IP address: {IPAddress}", ipAddress);

            // Call the Azure Maps Geolocation API
            var response = await _geolocationClient.GetCountryCodeAsync(parsedIP);

            if (response.Value != null)
            {
                var countryResult = response.Value;
                var result = new
                {
                    CountryInfo = new
                    {
                        IsoCode = countryResult.IsoCode,
                        CountryCode = countryResult.IsoCode // Alias for backward compatibility
                    },
                    RequestInfo = new
                    {
                        IPAddress = ipAddress,
                        IPAddressType = parsedIP.AddressFamily.ToString(),
                        Timestamp = DateTimeOffset.UtcNow
                    }
                };

                logger.LogInformation("Successfully retrieved country code: {CountryCode} for IP: {IPAddress}", 
                    countryResult.IsoCode, ipAddress);

                return JsonSerializer.Serialize(new { success = true, result }, new JsonSerializerOptions { WriteIndented = true });
            }

            logger.LogWarning("No country code data returned for IP address: {IPAddress}", ipAddress);
            return JsonSerializer.Serialize(new { success = false, message = "No country code data returned for the provided IP address" });
        }
        catch (FormatException ex)
        {
            logger.LogError(ex, "Invalid IP address format: {IPAddress}", ipAddress);
            return JsonSerializer.Serialize(new { error = $"Invalid IP address format: '{ipAddress}'. Please provide a valid IPv4 or IPv6 address." });
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Maps API error during geolocation lookup: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = $"API Error: {ex.Message}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during geolocation lookup");
            return JsonSerializer.Serialize(new { error = "An unexpected error occurred" });
        }
    }

    /// <summary>
    /// Get location information for multiple IP addresses
    /// </summary>
    [Function(nameof(GetCountryCodeBatch))]
    public async Task<string> GetCountryCodeBatch(
        [McpToolTrigger(
            "get_country_code_batch",
            "Get country codes for multiple IP addresses in a single request. Useful for batch processing of IP addresses."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "ipAddresses",
            "string",
            "JSON array of IP addresses to look up. Format: '[\"8.8.8.8\", \"1.1.1.1\", \"2001:4898:80e8:b::189\"]'. Maximum 100 IP addresses per request."
        )] string ipAddresses
    )
    {
        try
        {
            var ipList = JsonSerializer.Deserialize<List<string>>(ipAddresses);
            
            if (ipList == null || ipList.Count == 0)
            {
                return JsonSerializer.Serialize(new { error = "At least one IP address is required" });
            }

            if (ipList.Count > 100)
            {
                return JsonSerializer.Serialize(new { error = "Maximum 100 IP addresses allowed per batch request" });
            }

            logger.LogInformation("Processing batch geolocation request for {Count} IP addresses", ipList.Count);

            var results = new List<object>();
            var successCount = 0;
            var errorCount = 0;

            foreach (var ip in ipList)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(ip))
                    {
                        results.Add(new
                        {
                            IPAddress = ip,
                            Error = "Empty or null IP address",
                            CountryCode = (string?)null
                        });
                        errorCount++;
                        continue;
                    }

                    if (!IPAddress.TryParse(ip, out var parsedIP))
                    {
                        results.Add(new
                        {
                            IPAddress = ip,
                            Error = "Invalid IP address format",
                            CountryCode = (string?)null
                        });
                        errorCount++;
                        continue;
                    }

                    var response = await _geolocationClient.GetCountryCodeAsync(parsedIP);
                    
                    if (response.Value != null)
                    {
                        results.Add(new
                        {
                            IPAddress = ip,
                            CountryCode = response.Value.IsoCode,
                            IPAddressType = parsedIP.AddressFamily.ToString(),
                            Error = (string?)null
                        });
                        successCount++;
                    }
                    else
                    {
                        results.Add(new
                        {
                            IPAddress = ip,
                            Error = "No country code data returned",
                            CountryCode = (string?)null
                        });
                        errorCount++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error processing IP address {IP}: {Message}", ip, ex.Message);
                    results.Add(new
                    {
                        IPAddress = ip,
                        Error = ex.Message,
                        CountryCode = (string?)null
                    });
                    errorCount++;
                }
            }

            var result = new
            {
                Summary = new
                {
                    TotalRequests = ipList.Count,
                    SuccessfulRequests = successCount,
                    FailedRequests = errorCount,
                    ProcessedAt = DateTimeOffset.UtcNow
                },
                Results = results
            };

            logger.LogInformation("Completed batch geolocation request: {Success}/{Total} successful", 
                successCount, ipList.Count);

            return JsonSerializer.Serialize(new { success = true, result }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid JSON format for IP addresses");
            return JsonSerializer.Serialize(new { error = "Invalid JSON format for IP addresses. Expected array of strings." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during batch geolocation lookup");
            return JsonSerializer.Serialize(new { error = "An unexpected error occurred during batch processing" });
        }
    }

    /// <summary>
    /// Validate IP address format and get basic information
    /// </summary>
    [Function(nameof(ValidateIPAddress))]
    public Task<string> ValidateIPAddress(
        [McpToolTrigger(
            "validate_ip_address",
            "Validate IP address format and get basic information about the IP address type, family, and format."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "ipAddress",
            "string",
            "The IP address to validate (IPv4 or IPv6). Examples: '8.8.8.8', '2001:4898:80e8:b::189'"
        )] string ipAddress
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return Task.FromResult(JsonSerializer.Serialize(new { error = "IP address is required" }));
            }

            if (!IPAddress.TryParse(ipAddress, out var parsedIP))
            {
                return Task.FromResult(JsonSerializer.Serialize(new { 
                    error = $"Invalid IP address format: '{ipAddress}'. Please provide a valid IPv4 or IPv6 address.",
                    isValid = false
                }));
            }

            var result = new
            {
                ValidationResult = new
                {
                    IsValid = true,
                    IPAddress = ipAddress,
                    AddressFamily = parsedIP.AddressFamily.ToString(),
                    IsIPv4 = parsedIP.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork,
                    IsIPv6 = parsedIP.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6,
                    IsLoopback = IPAddress.IsLoopback(parsedIP),
                    IsPrivate = IsPrivateIP(parsedIP),
                    Scope = parsedIP.IsIPv6LinkLocal ? "Link-Local" : 
                           parsedIP.IsIPv6SiteLocal ? "Site-Local" : 
                           parsedIP.IsIPv6Multicast ? "Multicast" : "Global"
                },
                TechnicalInfo = new
                {
                    Bytes = parsedIP.GetAddressBytes(),
                    ScopeId = parsedIP.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? parsedIP.ScopeId : (long?)null,
                    Timestamp = DateTimeOffset.UtcNow
                }
            };

            logger.LogInformation("Successfully validated IP address: {IPAddress} (Type: {Type})", 
                ipAddress, parsedIP.AddressFamily);

            return Task.FromResult(JsonSerializer.Serialize(new { success = true, result }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during IP address validation");
            return Task.FromResult(JsonSerializer.Serialize(new { error = "An unexpected error occurred during validation" }));
        }
    }

    /// <summary>
    /// Helper method to determine if an IP address is private
    /// </summary>
    private static bool IsPrivateIP(IPAddress ip)
    {
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            // 10.0.0.0/8
            if (bytes[0] == 10) return true;
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            // 127.0.0.0/8 (loopback)
            if (bytes[0] == 127) return true;
        }
        else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            // IPv6 private ranges
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || IPAddress.IsLoopback(ip);
        }
        
        return false;
    }
}
