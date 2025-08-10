// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using Azure.Maps.Mcp.Common;
using CountryData.Standard;
using System.Text.Json;

namespace Azure.Maps.Mcp.Tools;

/// <summary>
/// Country-related utilities split out to keep other tools small and focused
/// </summary>
public class CountryTool : BaseMapsTool
{
    private readonly CountryHelper _countryHelper;

    public CountryTool(ILogger<CountryTool> logger, CountryHelper countryHelper, Azure.Maps.Mcp.Services.IAzureMapsService mapsService)
        : base(mapsService, logger)
    {
        _countryHelper = countryHelper;
    }

    /// <summary>
    /// Get country info by ISO code (alpha-2 or alpha-3)
    /// </summary>
    [Function(nameof(GetCountryInfo))]
    public Task<string> GetCountryInfo(
        [McpToolTrigger(
            "search_country_info",
            "Country info by ISO 3166-1 alpha-2/alpha-3 code (e.g., US, USA)."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "countryCode",
            "string",
            "ISO 3166-1 alpha-2/alpha-3. Examples: US, USA, GB, GBR."
        )] string countryCode)
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(countryCode))
            return Task.FromResult(ResponseHelper.CreateErrorResponse("Country code is required"));

        var code = countryCode.Trim();
        if (code.Length is < 2 or > 3 || !code.All(char.IsLetter))
            return Task.FromResult(ResponseHelper.CreateErrorResponse("Country code must be 2 or 3 letters"));

        // Normalize to alpha-2 if alpha-3 provided
        if (code.Length == 3)
        {
            var two = ToolsHelper.ConvertThreeLetterToTwoLetter(code);
            if (!string.IsNullOrEmpty(two)) code = two;
        }

        code = code.ToUpperInvariant();
        var country = _countryHelper.GetCountryByCode(code);
        if (country == null)
        {
            // Simple suggestions by similarity
            try
            {
                var suggestions = _countryHelper.GetCountryData()
                    .Select(c => new { c.CountryShortCode, c.CountryName, sim = ToolsHelper.CalculateStringSimilarity(c.CountryShortCode, code) })
                    .Where(x => x.sim > 0.5)
                    .OrderByDescending(x => x.sim)
                    .Take(5)
                    .Select(x => new { code = x.CountryShortCode, name = x.CountryName })
                    .ToList();

                return Task.FromResult(ResponseHelper.CreateErrorResponse($"No country found for '{code}'", new { suggestions }));
            }
            catch
            {
                return Task.FromResult(ResponseHelper.CreateErrorResponse($"No country found for '{code}'"));
            }
        }

    _logger.LogInformation("Country found: {Code} - {Name}", country.CountryShortCode, country.CountryName);
        return Task.FromResult(ResponseHelper.CreateSuccessResponse(new
        {
            country = new { code = country.CountryShortCode, name = country.CountryName }
        }));
    }

    /// <summary>
    /// Search countries by name or code
    /// </summary>
    [Function(nameof(FindCountries))]
    public Task<string> FindCountries(
        [McpToolTrigger(
            "search_countries",
            "Search countries by name or code."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "searchTerm",
            "string",
            "Country name or code. Examples: 'Uni', 'US', 'DE'"
        )] string searchTerm,
        [McpToolProperty(
            "maxResults",
            "number",
            "1..50 (default 10)"
        )] int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return Task.FromResult(ResponseHelper.CreateErrorResponse("Search term is required"));

        var normalized = searchTerm.Trim();
        if (normalized.Length < 2)
            return Task.FromResult(ResponseHelper.CreateErrorResponse("Search term must be at least 2 characters"));

        var range = ValidationHelper.ValidateRange(maxResults, 1, 50, nameof(maxResults));
        maxResults = range.NormalizedValue;

        var results = _countryHelper.GetCountryData()
            .Select(c => new { code = c.CountryShortCode, name = c.CountryName })
            .Where(c => c.code.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                        c.name.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.name)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(ResponseHelper.CreateSuccessResponse(new
        {
            term = normalized,
            count = results.Count,
            results
        }));
    }
}
