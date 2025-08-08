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
using System.Text.Json;
using CountryData.Standard;

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
            "geolocation_ip",
            "Get country code and location information for a given IP address. Supports both IPv4 and IPv6 addresses. Returns country code, name, and continent information."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "ipAddress",
            "string",
            "The IP address to look up (IPv4 or IPv6). Examples: '8.8.8.8', '1.1.1.1', '2001:4898:80e8:b::189'. Private/local addresses cannot be geolocated."
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
                var helper = new CountryHelper();
                var country = helper.GetCountryByCode(response.Value.IsoCode);
                
                if (country != null)
                {
                    var result = ResponseHelper.CreateCountryInfo(
                        country.CountryShortCode, 
                        country.CountryName);
                    
                    logger.LogInformation("Successfully retrieved country: {CountryCode} for IP: {IPAddress}", 
                        country.CountryShortCode, ipAddress);
                    
                    return ResponseHelper.CreateSuccessResponse(result);
                }
            }

            return ResponseHelper.CreateErrorResponse("No country data available for this IP address");
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
            "Get country codes for multiple IP addresses. Processes up to 100 IP addresses efficiently with parallel processing."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "ipAddresses",
            "array",
            "Array of IP addresses to look up. Maximum 100 addresses. Examples: [\"8.8.8.8\", \"1.1.1.1\"]"
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
            
            var successful = results.Where(r => r.IsSuccess).ToList();
            var failed = results.Where(r => !r.IsSuccess).Select(r => new { r.IPAddress, r.Error }).ToList();

            var response = ResponseHelper.CreateBatchSummary(successful, failed.Cast<object>().ToList(), ipAddresses.Length);
            
            logger.LogInformation("Completed batch geolocation: {Success}/{Total} successful", 
                successful.Count, uniqueIPs.Count);

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
        var helper = new CountryHelper();
        var semaphore = new SemaphoreSlim(10, 10);
        
        var tasks = ipAddresses.Select(async ip =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await ProcessSingleIPAddress(ip, helper);
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
    private async Task<GeolocationResult> ProcessSingleIPAddress(string ip, CountryHelper helper)
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

            var response = await _geolocationClient.GetCountryCodeAsync(validation.ParsedIP!);

            if (response.Value?.IsoCode != null)
            {
                var country = helper.GetCountryByCode(response.Value.IsoCode);
                
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
            "Validate IP address format and get basic information about the IP address. Returns validation status and address type."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "ipAddress",
            "string",
            "The IP address to validate. Examples: '8.8.8.8' (IPv4), '2001:4898:80e8:b::189' (IPv6)."
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
                IsValid = true,
                IPAddress = ipAddress,
                AddressFamily = parsedIP.AddressFamily.ToString(),
                IsIPv4 = parsedIP.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork,
                IsIPv6 = parsedIP.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6,
                IsLoopback = IPAddress.IsLoopback(parsedIP),
                IsPrivate = ValidationHelper.IsPrivateIP(parsedIP),
                CanGeolocate = !ValidationHelper.IsPrivateIP(parsedIP) && !IPAddress.IsLoopback(parsedIP)
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
