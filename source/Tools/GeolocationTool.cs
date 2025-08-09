// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using Azure.Maps.Mcp.Services;
using Azure.Maps.Mcp.Common;
using Azure.Maps.Geolocation;
using Azure;
using System.Net;
using CountryData.Standard;
using System.Text.Json;

namespace Azure.Maps.Mcp.Tools;

/// <summary>
/// Azure Maps Geolocation Tool providing IP-based geolocation and country code lookup capabilities
/// </summary>
public class GeolocationTool(IAzureMapsService azureMapsService, ILogger<GeolocationTool> logger)
{
    private readonly MapsGeolocationClient _geolocationClient = azureMapsService.GeolocationClient;
    private readonly CountryHelper _countryHelper = new();

    /// <summary>
    /// Get country code and location information for an IP address with enhanced context
    /// </summary>
    [Function(nameof(GetCountryCodeByIP))]
    public async Task<string> GetCountryCodeByIP(
        [McpToolTrigger(
            "geolocation_ip",
            "Country code lookup for a public IP (IPv4/IPv6). Returns ISO 3166-1 alpha-2. Example: 8.8.8.8 -> US. Private/loopback IPs are not supported."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "ipAddress",
            "string",
            "Public IPv4/IPv6. Examples: 8.8.8.8, 1.1.1.1, 2001:4860:4860::8888. Private/loopback not supported."
        )] string ipAddress
    )
    {
        try
        {
            // Validate input
            var validation = ValidationHelper.ValidateIPAddress(ipAddress);
            if (!validation.IsValid)
                return ResponseHelper.CreateErrorResponse(validation.ErrorMessage!);

            var parsedIP = validation.ParsedIP!;
            
            // Check if IP is suitable for geolocation
            if (ValidationHelper.IsPrivateIP(parsedIP) || IPAddress.IsLoopback(parsedIP))
            {
                var message = IPAddress.IsLoopback(parsedIP) 
                    ? "Loopback address refers to local machine and cannot be geolocated"
                    : "Private IP address cannot be geolocated";
                return ResponseHelper.CreateErrorResponse(message);
            }

            logger.LogInformation("Processing geolocation request for IP: {IPAddress}", ipAddress);

        // Call Azure Maps API
        var response = await _geolocationClient.GetCountryCodeAsync(parsedIP);

        if (response?.Value?.IsoCode != null)
        {
            var country = _countryHelper.GetCountryByCode(response.Value.IsoCode);
            
            if (country != null)
            {
                var aiOptimizedResult = new
                {
                    success = true,
                    tool = "geolocation_ip",
                    timestamp = DateTime.UtcNow.ToString("O"),
                    query = new
                    {
                        ipAddress = ipAddress,
                        ipType = parsedIP.AddressFamily.ToString(),
                        isPublic = !ValidationHelper.IsPrivateIP(parsedIP) && !IPAddress.IsLoopback(parsedIP)
                    },
                    location = new
                    {
                        country = new
                        {
                            code = country.CountryShortCode,
                            name = country.CountryName
                        },
                        geographicContext = new
                        {
                            dataSource = "Azure Maps IP Geolocation Database",
                            accuracy = "Country-level identification"
                        }
                    },
                    aiContext = new
                    {
                        toolCategory = "IP_ANALYSIS",
                        nextSuggestedActions = new[]
                        {
                            "Use country information for content localization",
                            "Apply region-specific business logic",
                            "Consider timezone for scheduling and notifications",
                            "Use for geographic analytics and user segmentation"
                        },
                        usageHints = new[]
                        {
                            "IP geolocation provides country-level accuracy",
                            "Not suitable for precise location tracking",
                            "Respects user privacy by not tracking exact location",
                            "Best for regional content delivery and compliance"
                        },
                        qualityIndicators = new
                        {
                            accuracy = "Country-level (high confidence)",
                            dataSource = "Azure Maps IP Geolocation",
                            privacyCompliant = true
                        }
                    }
                };
                
                logger.LogInformation("Successfully retrieved country: {CountryCode} for IP: {IPAddress}", 
                    country.CountryShortCode, ipAddress);
                
                return JsonSerializer.Serialize(aiOptimizedResult, new JsonSerializerOptions { WriteIndented = false });
            }
        }

        // No results found
        var noResultsResponse = new
        {
            success = false,
            tool = "geolocation_ip",
            timestamp = DateTime.UtcNow.ToString("O"),
            error = new
            {
                type = "NO_RESULTS",
                message = "No country data available for this IP address",
                ipAddress = ipAddress,
                recovery = new
                {
                    immediateActions = new[]
                    {
                        "Verify the IP address is correct and public",
                        "Check if IP is from a known public range",
                        "Try with a different public IP address"
                    },
                    commonCauses = new[]
                    {
                        "IP address not in geolocation database",
                        "Recently allocated IP ranges", 
                        "Special purpose IP addresses (satellite, military)"
                    },
                    alternatives = new[]
                    {
                        "Use batch IP geolocation for multiple addresses",
                        "Validate IP format first",
                        "Consider fallback to user-provided location"
                    }
                }
            }
        };
        
        return JsonSerializer.Serialize(noResultsResponse, new JsonSerializerOptions { WriteIndented = false });
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Maps API error for IP: {IPAddress}", ipAddress);
            return ResponseHelper.CreateErrorResponse($"API Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during geolocation lookup for IP: {IPAddress}", ipAddress);
            return ResponseHelper.CreateErrorResponse("An unexpected error occurred");
        }
    }

    /// <summary>
    /// Get location information for multiple IP addresses
    /// </summary>
    [Function(nameof(GetCountryCodeBatch))]
    public async Task<string> GetCountryCodeBatch(
        [McpToolTrigger(
            "geolocation_ip_batch",
            "Country codes for up to 100 IPs in one call. Returns ISO 3166-1 alpha-2."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "ipAddresses",
            "array",
            "Array of IPv4/IPv6 strings (max 100). Duplicates ignored."
        )] string[] ipAddresses
    )
    {
        try
        {
            // Validate input
            var validation = ValidationHelper.ValidateArrayInput(ipAddresses, 100, "IP address");
            if (!validation.IsValid)
                return ResponseHelper.CreateErrorResponse(validation.ErrorMessage!);

            var uniqueIPs = validation.UniqueValues!;
            
            logger.LogInformation("Processing batch geolocation for {Count} unique IP addresses", uniqueIPs.Count);

            // Process IPs in parallel
            var results = await ProcessIPAddressesBatch(uniqueIPs);
            
            var okItems = results.Where(r => r.IsSuccess && r.Country != null)
                .Select(r => new { ip = r.IPAddress, code = r.Country!.CountryShortCode, name = r.Country!.CountryName })
                .ToList();
            var failItems = results.Where(r => !r.IsSuccess)
                .Select(r => new { ip = r.IPAddress, err = r.Error })
                .ToList();

            var response = new
            {
                total = ipAddresses.Length,
                ok = okItems.Count,
                fail = failItems.Count,
                rate = ipAddresses.Length > 0 ? Math.Round((double)okItems.Count / ipAddresses.Length * 100, 1) : 0,
                items = new { ok = okItems, fail = failItems }
            };
            
            logger.LogInformation("Completed batch geolocation: {Success}/{Total} successful", 
                okItems.Count, uniqueIPs.Count);

            return ResponseHelper.CreateSuccessResponse(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during batch geolocation");
            return ResponseHelper.CreateErrorResponse("Batch processing error");
        }
    }

    /// <summary>
    /// Processes IP addresses with parallel optimization
    /// </summary>
    private async Task<List<GeolocationResult>> ProcessIPAddressesBatch(HashSet<string> ipAddresses)
    {
        var semaphore = new SemaphoreSlim(10, 10);
        
        var tasks = ipAddresses.Select(async ip =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await ProcessSingleIPAddress(ip);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    /// <summary>
    /// Processes a single IP address
    /// </summary>
    private async Task<GeolocationResult> ProcessSingleIPAddress(string ip)
    {
        try
        {
            var validation = ValidationHelper.ValidateIPAddress(ip);
            if (!validation.IsValid)
            {
                return new GeolocationResult
                {
                    IPAddress = ip,
                    IsSuccess = false,
                    Error = "Invalid IP address format"
                };
            }

            var parsedIP = validation.ParsedIP!;

            // Skip private and loopback addresses (can't be geolocated)
            if (ValidationHelper.IsPrivateIP(parsedIP) || IPAddress.IsLoopback(parsedIP))
            {
                var message = IPAddress.IsLoopback(parsedIP)
                    ? "Loopback address refers to local machine and cannot be geolocated"
                    : "Private IP address cannot be geolocated";

                return new GeolocationResult
                {
                    IPAddress = ip,
                    IsSuccess = false,
                    Error = message
                };
            }

            var response = await _geolocationClient.GetCountryCodeAsync(parsedIP);

            if (response?.Value?.IsoCode != null)
            {
                var country = _countryHelper.GetCountryByCode(response.Value.IsoCode);

                if (country != null)
                {
                    return new GeolocationResult
                    {
                        IPAddress = ip,
                        IsSuccess = true,
                        Country = country
                    };
                }

                return new GeolocationResult
                {
                    IPAddress = ip,
                    IsSuccess = false,
                    Error = "No country data available"
                };
            }

            return new GeolocationResult
            {
                IPAddress = ip,
                IsSuccess = false,
                Error = "No country data available"
            };
        }
        catch (Exception ex)
        {
            return new GeolocationResult
            {
                IPAddress = ip,
                IsSuccess = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Validate IP address format and get basic information
    /// </summary>
    [Function(nameof(ValidateIPAddress))]
    public Task<string> ValidateIPAddress(
        [McpToolTrigger(
            "geolocation_ip_validate",
            "Validate IPv4/IPv6 and basic traits (public/private, loopback)."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "ipAddress",
            "string",
            "IPv4/IPv6 string. Examples: 8.8.8.8, 2001:4898:80e8:b::189"
        )] string ipAddress
    )
    {
        try
        {
            var validation = ValidationHelper.ValidateIPAddress(ipAddress);
            if (!validation.IsValid)
                return Task.FromResult(ResponseHelper.CreateErrorResponse(validation.ErrorMessage!));

            var parsedIP = validation.ParsedIP!;
            var result = new
            {
                ok = true,
                ip = ipAddress,
                family = parsedIP.AddressFamily.ToString(),
                v4 = parsedIP.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork,
                v6 = parsedIP.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6,
                loopback = IPAddress.IsLoopback(parsedIP),
                @private = ValidationHelper.IsPrivateIP(parsedIP),
                geo = !ValidationHelper.IsPrivateIP(parsedIP) && !IPAddress.IsLoopback(parsedIP)
            };

            logger.LogInformation("Successfully validated IP address: {IPAddress}", ipAddress);
            return Task.FromResult(ResponseHelper.CreateSuccessResponse(result));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during IP validation");
            return Task.FromResult(ResponseHelper.CreateErrorResponse("Validation error"));
        }
    }

    /// <summary>
    /// Represents the result of processing a single IP address
    /// </summary>
    private class GeolocationResult
    {
        public string IPAddress { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public Country? Country { get; set; }
        public string? Error { get; set; }
    }
}
