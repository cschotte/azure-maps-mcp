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
public class GeolocationTool : BaseMapsTool
{
    private readonly MapsGeolocationClient _geolocationClient;
    private readonly CountryHelper _countryHelper;

    public GeolocationTool(IAzureMapsService mapsService, ILogger<GeolocationTool> logger, CountryHelper countryHelper)
        : base(mapsService, logger)
    {
        _geolocationClient = mapsService.GeolocationClient;
        _countryHelper = countryHelper;
    }

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
        return await ExecuteWithErrorHandling(async () =>
        {
            // Validate input
            var validation = ValidationHelper.ValidateIPAddress(ipAddress);
            if (!validation.IsValid)
                throw new ArgumentException(validation.ErrorMessage);

            var parsedIP = validation.ParsedIP!;

            // Check if IP is suitable for geolocation
            if (ValidationHelper.IsPrivateIP(parsedIP) || IPAddress.IsLoopback(parsedIP))
            {
                var message = IPAddress.IsLoopback(parsedIP)
                    ? "Loopback address refers to local machine and cannot be geolocated"
                    : "Private IP address cannot be geolocated";
                throw new ArgumentException(message);
            }

            _logger.LogInformation("Processing geolocation request for IP: {IPAddress}", ipAddress);

            // Call Azure Maps API
            var response = await _geolocationClient.GetCountryCodeAsync(parsedIP);

            if (response?.Value?.IsoCode != null)
            {
                var country = _countryHelper.GetCountryByCode(response.Value.IsoCode);
                if (country != null)
                {
                    _logger.LogInformation("Country for IP resolved: {Code}", country.CountryShortCode);
                    return new
                    {
                        query = new { ipAddress, ipType = parsedIP.AddressFamily.ToString(), isPublic = true },
                        country = new { code = country.CountryShortCode, name = country.CountryName }
                    };
                }
            }

            throw new InvalidOperationException("No country data available for this IP address");

        }, nameof(GetCountryCodeByIP), new { ipAddress });
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
        return await ExecuteWithErrorHandling(async () =>
        {
            // Validate input
            var validation = ValidationHelper.ValidateArrayInput(ipAddresses, 100, "IP address");
            if (!validation.IsValid)
                throw new ArgumentException(validation.ErrorMessage);

            var uniqueIPs = validation.UniqueValues!;

            _logger.LogInformation("Processing batch geolocation for {Count} unique IP addresses", uniqueIPs.Count);

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

            _logger.LogInformation("Completed batch geolocation: {Success}/{Total} successful",
                okItems.Count, uniqueIPs.Count);

            return response;
        }, nameof(GetCountryCodeBatch), new { count = ipAddresses?.Length ?? 0 });
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
    public async Task<string> ValidateIPAddress(
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
        return await ExecuteWithErrorHandling(async () =>
        {
            var validation = ValidationHelper.ValidateIPAddress(ipAddress);
            if (!validation.IsValid)
                throw new ArgumentException(validation.ErrorMessage);

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

            _logger.LogInformation("Successfully validated IP address: {IPAddress}", ipAddress);
            return await Task.FromResult(result);
        }, nameof(ValidateIPAddress), new { ipAddress });
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
