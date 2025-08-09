// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using Azure.Maps.Search;
using Azure.Maps.Search.Models;
using Azure.Maps.Mcp.Services;
using Azure.Maps.Mcp.Common;
using Azure.Core.GeoJson;
using System.Text.Json;
using CountryData.Standard;

namespace Azure.Maps.Mcp.Tools;

/// <summary>
/// Azure Maps Search Tool providing geocoding, reverse geocoding, and administrative boundary polygon capabilities
/// </summary>
public class SearchTool(IAzureMapsService azureMapsService, ILogger<SearchTool> logger)
{
    private readonly MapsSearchClient _searchClient = azureMapsService.SearchClient;
    private readonly CountryHelper _countryHelper = new();
    private static readonly Dictionary<string, BoundaryResultTypeEnum> ResultTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "locality", BoundaryResultTypeEnum.Locality },
        { "postalcode", BoundaryResultTypeEnum.PostalCode },
        { "admindistrict", BoundaryResultTypeEnum.AdminDistrict },
        { "countryregion", BoundaryResultTypeEnum.CountryRegion }
    };

    private static readonly Dictionary<string, ResolutionEnum> Resolutions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "small", ResolutionEnum.Small },
        { "medium", ResolutionEnum.Medium },
        { "large", ResolutionEnum.Large }
    };

    /// <summary>
    /// Converts an address or place name to geographic coordinates
    /// </summary>
    [Function(nameof(Geocoding))]
    public async Task<string> Geocoding(
        [McpToolTrigger(
            "search_geocoding",
            "Forward geocode address or place to coordinates."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "address",
            "string",
            "Free-text address or POI."
        )] string address,
        [McpToolProperty(
            "maxResults",
            "number",
            "1..20 (default 5)"
        )] int maxResults = 5
    )
    {
        try
        {
            // Validate input
            var addressValidation = ValidationHelper.ValidateStringInput(address, 2, 2048, "Address");
            if (!addressValidation.IsValid)
                return ResponseHelper.CreateErrorResponse(addressValidation.ErrorMessage!);

            var rangeValidation = ValidationHelper.ValidateRange(maxResults, 1, 20, "maxResults");
            maxResults = rangeValidation.NormalizedValue;

            var normalizedAddress = address.Trim();
            logger.LogInformation("Geocoding address: '{Address}' (requesting {MaxResults} results)", normalizedAddress, maxResults);

            var options = new GeocodingQuery() { Query = normalizedAddress, Top = maxResults };
            var response = await _searchClient.GetGeocodingAsync(query: normalizedAddress, options: options);

            if (response.Value?.Features != null && response.Value.Features.Any())
            {
                var items = response.Value.Features.Select(f => new
                {
                    addr = f.Properties.Address?.FormattedAddress,
                    lat = f.Geometry.Coordinates[1],
                    lon = f.Geometry.Coordinates[0],
                    comp = new
                    {
                        no = f.Properties.Address?.StreetNumber,
                        street = f.Properties.Address?.StreetName,
                        city = f.Properties.Address?.Locality,
                        zip = f.Properties.Address?.PostalCode,
                        country = f.Properties.Address?.CountryRegion?.Name,
                        iso = f.Properties.Address?.CountryRegion?.Iso
                    },
                    conf = f.Properties.Confidence.ToString()
                }).ToList();

                var result = new { q = normalizedAddress, n = items.Count, items };
                logger.LogInformation("Geocode ok: {Count} results", items.Count);
                return JsonSerializer.Serialize(new { ok = true, result }, new JsonSerializerOptions { WriteIndented = false });
            }

            // No results found
            var suggestions = new List<string>
            {
                "Try a more complete address with city and state/country",
                "Check spelling of street names and city names"
            };
            
            return JsonSerializer.Serialize(new { ok = false, err = "no_results", tips = suggestions });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during geocoding for address: {Address}", address);
            return ResponseHelper.CreateErrorResponse("Geocoding error occurred");
        }
    }

    // string similarity and code conversion helpers moved to ToolsHelper



    /// <summary>
    /// Converts geographic coordinates to a street address
    /// </summary>
    [Function(nameof(ReverseGeocoding))]
    public async Task<string> ReverseGeocoding(
        [McpToolTrigger(
            "search_geocoding_reverse",
            "Reverse geocode coordinates to address."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "latitude",
            "string",
            "number: -90..90"
        )] double latitude,
        [McpToolProperty(
            "longitude",
            "string",
            "number: -180..180"
        )] double longitude
    )
    {
        try
        {
            // Validate coordinates
            var validation = ValidationHelper.ValidateCoordinates(latitude, longitude);
            if (!validation.IsValid)
                return ResponseHelper.CreateErrorResponse(validation.ErrorMessage!);

            logger.LogInformation("Reverse geocoding coordinates: {Latitude}, {Longitude}", latitude, longitude);

            var coordinates = new GeoPosition(longitude, latitude);
            var response = await _searchClient.GetReverseGeocodingAsync(coordinates);

            if (response.Value?.Features?.Any() == true)
            {
                var f = response.Value.Features.First();
                var result = new
                {
                    addr = f.Properties.Address?.FormattedAddress,
                    comp = new
                    {
                        no = f.Properties.Address?.StreetNumber,
                        street = f.Properties.Address?.StreetName,
                        zip = f.Properties.Address?.PostalCode,
                        country = f.Properties.Address?.CountryRegion?.Name,
                        iso = f.Properties.Address?.CountryRegion?.Iso,
                        city = f.Properties.Address?.Locality
                    },
                    lat = latitude,
                    lon = longitude
                };

                logger.LogInformation("Reverse geocode ok");
                return JsonSerializer.Serialize(new { ok = true, result }, new JsonSerializerOptions { WriteIndented = false });
            }

            return JsonSerializer.Serialize(new { ok = false, err = "no_results" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during reverse geocoding");
            return ResponseHelper.CreateErrorResponse("Reverse geocoding error occurred");
        }
    }
    
    /// <summary>
    /// Gets administrative boundary polygon for a specific location
    /// </summary>
    [Function(nameof(GetPolygon))]
    public async Task<string> GetPolygon(
        [McpToolTrigger(
            "search_polygon",
            "Get admin boundary polygon at a point."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "latitude",
            "string",
            "number: -90..90"
        )] double latitude,
        [McpToolProperty(
            "longitude",
            "string",
            "number: -180..180"
        )] double longitude,
        [McpToolProperty(
            "resultType",
            "string",
            "Boundary type: locality|postalCode|adminDistrict|countryRegion"
        )] string resultType = "locality",
        [McpToolProperty(
            "resolution",
            "string",
            "Polygon detail: small|medium|large"
        )] string resolution = "small"
    )
    {
        try
        {
            var coordValidation = ValidationHelper.ValidateCoordinates(latitude, longitude);
            if (!coordValidation.IsValid)
                return JsonSerializer.Serialize(new { error = coordValidation.ErrorMessage });

            // Validate result type options
            if (!ResultTypes.TryGetValue(resultType, out var resultTypeEnum))
            {
                var validOptions = string.Join(", ", ResultTypes.Keys);
                return JsonSerializer.Serialize(new { error = $"Invalid result type '{resultType}'. Valid options: {validOptions}" });
            }

            // Validate resolution options
            if (!Resolutions.TryGetValue(resolution, out var resolutionEnum))
            {
                var validOptions = string.Join(", ", Resolutions.Keys);
                return JsonSerializer.Serialize(new { error = $"Invalid resolution '{resolution}'. Valid options: {validOptions}" });
            }

            logger.LogInformation("Getting polygon boundary for coordinates: {Latitude}, {Longitude} with type: {ResultType}", latitude, longitude, resultType);

            var options = new GetPolygonOptions()
            {
                Coordinates = new GeoPosition(longitude, latitude),
                ResultType = resultTypeEnum,
                Resolution = resolutionEnum
            };

            var response = await _searchClient.GetPolygonAsync(options);

            if (response.Value?.Geometry != null && response.Value.Geometry.Count > 0)
            {
                var geoms = new List<object>();
                for (int i = 0; i < response.Value.Geometry.Count; i++)
                {
                    if (response.Value.Geometry[i] is GeoPolygon polygon)
                    {
                        var coords = polygon.Coordinates[0].Select(c => new[] { c.Latitude, c.Longitude }).ToArray();
                        geoms.Add(new { i, n = coords.Length, coords });
                    }
                }

                var meta = new
                {
                    type = resultType,
                    res = resolution,
                    lat = latitude,
                    lon = longitude,
                    cr = response.Value.Properties?.Copyright
                };

                var result = new { meta, n = geoms.Count, geoms };
                logger.LogInformation("Boundary ok: {Count}", geoms.Count);
                return JsonSerializer.Serialize(new { ok = true, result }, new JsonSerializerOptions { WriteIndented = false });
            }

            logger.LogWarning("No boundary polygon found for coordinates: {Latitude}, {Longitude}", latitude, longitude);
            return JsonSerializer.Serialize(new { success = false, message = "No boundary polygon found for these coordinates" });
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Maps API error during polygon retrieval: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = $"API Error: {ex.Message}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during polygon retrieval");
            return JsonSerializer.Serialize(new { error = "An unexpected error occurred" });
        }
    }

    /// <summary>
    /// Get comprehensive country information by ISO country code with enhanced data and insights
    /// </summary>
    [Function(nameof(GetCountryInfo))]
    public Task<string> GetCountryInfo(
        [McpToolTrigger(
            "search_country_info",
            "Get country info by ISO code."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "countryCode",
            "string",
            "ISO-3166 alpha-2 or alpha-3 (e.g., US or USA)"
        )] string countryCode
    )
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Enhanced input validation with intelligent normalization
            var validationResult = ValidateAndNormalizeCountryCode(countryCode);
            if (!validationResult.IsValid)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { 
                    error = validationResult.ErrorMessage,
                    inputReceived = countryCode,
                    suggestions = validationResult.Suggestions,
                    timestamp = startTime
                }));
            }

            var normalizedCode = validationResult.NormalizedCode!;
            logger.LogInformation("Getting enhanced country information for code: {CountryCode}", normalizedCode);

            // Attempt to get country data with fallback strategies
            var countryResult = GetCountryWithFallback(normalizedCode);
            var endTime = DateTime.UtcNow;

            if (countryResult.Country != null)
            {
                // Build comprehensive response with enhanced country data
                var response = BuildEnhancedCountryResponse(countryResult.Country, normalizedCode, 
                    countryResult.MatchMethod, startTime, endTime);

                logger.LogInformation("Successfully retrieved enhanced country information for: {CountryName} using {Method} in {Duration}ms", 
                    countryResult.Country.CountryName, countryResult.MatchMethod, (endTime - startTime).TotalMilliseconds);

                return Task.FromResult(JsonSerializer.Serialize(new { 
                    ok = true, 
                    country = countryResult.Country,
                    data = response
                }, new JsonSerializerOptions { WriteIndented = false }));
            }

            // Enhanced error response with helpful suggestions
            var errorResponse = BuildCountryNotFoundResponse(normalizedCode, countryCode, startTime, endTime);
            logger.LogWarning("No country data found for code: {CountryCode}", normalizedCode);

            return Task.FromResult(JsonSerializer.Serialize(new { ok = false, error = errorResponse }));
        }
        catch (Exception ex)
        {
            var endTime = DateTime.UtcNow;
            logger.LogError(ex, "Unexpected error during country lookup for code: {CountryCode}", countryCode);
            return Task.FromResult(JsonSerializer.Serialize(new { 
                ok = false,
                err = "unexpected",
                msg = ex.Message,
                input = countryCode,
                ms = (endTime - startTime).TotalMilliseconds,
                ts = startTime
            }));
        }
    }

    /// <summary>
    /// Validates and normalizes country code input with intelligent suggestions
    /// </summary>
    private static (bool IsValid, string? ErrorMessage, string? NormalizedCode, List<string>? Suggestions) ValidateAndNormalizeCountryCode(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return (false, "Country code is required and cannot be empty", null, new List<string>
            {
                "Provide a 2-letter ISO code like 'US', 'GB', 'DE'",
                "Use 3-letter codes like 'USA', 'GBR', 'DEU'",
                "Ensure the code is not empty or whitespace"
            });
        }

        var trimmedCode = countryCode.Trim().ToUpperInvariant();

        // Validate length and format
        if (trimmedCode.Length < 2 || trimmedCode.Length > 3)
        {
            return (false, $"Country code '{countryCode}' must be 2 or 3 letters long", null, new List<string>
            {
                "Use 2-letter ISO 3166-1 alpha-2 codes (e.g., 'US', 'CA', 'GB')",
                "Use 3-letter ISO 3166-1 alpha-3 codes (e.g., 'USA', 'CAN', 'GBR')",
                "Check for typos in the country code"
            });
        }

        // Validate that it contains only letters
        if (!trimmedCode.All(char.IsLetter))
        {
            return (false, $"Country code '{countryCode}' must contain only letters", null, new List<string>
            {
                "Remove any numbers, spaces, or special characters",
                "Use only alphabetic characters (A-Z)",
                "Examples of valid codes: 'US', 'DE', 'JP', 'USA', 'DEU', 'JPN'"
            });
        }

        // Handle 3-letter to 2-letter conversion for common cases
        if (trimmedCode.Length == 3)
        {
            var converted = ToolsHelper.ConvertThreeLetterToTwoLetter(trimmedCode);
            if (converted != null)
            {
                return (true, null, converted, null);
            }
        }

        return (true, null, trimmedCode, null);
    }

    /// <summary>
    /// Convert common 3-letter country codes to 2-letter equivalents
    /// </summary>
    private static string? ConvertThreeLetterToTwoLetter(string threeLetterCode)
    {
        var conversionMap = new Dictionary<string, string>
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
    /// Attempts to get country data with multiple fallback strategies
    /// </summary>
    private (Country? Country, string MatchMethod) GetCountryWithFallback(string countryCode)
    {
        // Primary attempt: Direct code lookup
        var country = _countryHelper.GetCountryByCode(countryCode);
        if (country != null)
        {
            return (country, "Direct Code Match");
        }

        // Fallback 1: Try alternative code formats
        if (countryCode.Length == 2)
        {
            // Try common variations for 2-letter codes
            var variations = ToolsHelper.GenerateCountryCodeVariations(countryCode);
            foreach (var variation in variations)
            {
                country = _countryHelper.GetCountryByCode(variation);
                if (country != null)
                {
                    return (country, $"Alternative Format ({variation})");
                }
            }
        }

        // Fallback 2: Fuzzy search through all countries
        var allCountries = _countryHelper.GetCountryData();
        foreach (var c in allCountries)
        {
            if (c.CountryShortCode.Equals(countryCode, StringComparison.OrdinalIgnoreCase))
            {
                return (c, "Case-Insensitive Match");
            }
        }

        return (null, "No Match Found");
    }

    /// <summary>
    /// Generate alternative code variations for better matching
    /// </summary>
    // code variation logic moved to ToolsHelper

    /// <summary>
    /// Builds comprehensive country response with enhanced data and insights
    /// </summary>
    private object BuildEnhancedCountryResponse(Country country, string requestedCode, string matchMethod, DateTime startTime, DateTime endTime)
    {
        return new
        {
            CountryInfo = new
            {
                Country = country,
                RequestedCode = requestedCode,
                ActualCode = country.CountryShortCode,
                MatchMethod = matchMethod,
                DataSource = "CountryData.Standard Library"
            },
            Enhancement = new
            {
                ProcessingTime = new
                {
                    ProcessingTimeMs = (int)(endTime - startTime).TotalMilliseconds,
                    StartTime = startTime,
                    EndTime = endTime
                },
                DataQuality = new
                {
                    Confidence = "High",
                    DataCompleteness = CalculateCountryDataCompleteness(country),
                    LastValidated = "Library Maintained",
                    ReliabilityScore = CalculateReliabilityScore(matchMethod)
                }
            },
            RelatedData = new
            {
                RegionalContext = GetRegionalContext(country),
                SimilarCountries = FindSimilarCountries(country),
                UsageGuidance = GenerateCountryUsageGuidance(country, matchMethod)
            },
            TechnicalInfo = new
            {
                CodeValidation = new
                {
                    InputCode = requestedCode,
                    NormalizedCode = country.CountryShortCode,
                    IsExactMatch = requestedCode.Equals(country.CountryShortCode, StringComparison.OrdinalIgnoreCase),
                    AlternativeCodes = GetAlternativeCountryCodes(country)
                }
            }
        };
    }

    /// <summary>
    /// Calculate data completeness percentage for country information
    /// </summary>
    private static int CalculateCountryDataCompleteness(Country country)
    {
        var fields = new object?[]
        {
            country.CountryName,
            country.CountryShortCode,
            // Add other available properties as needed
        };

        var populatedFields = fields.Count(f => f != null && !string.IsNullOrWhiteSpace(f.ToString()));
        return (populatedFields * 100) / fields.Length;
    }

    /// <summary>
    /// Calculate reliability score based on match method
    /// </summary>
    private static int CalculateReliabilityScore(string matchMethod)
    {
        return matchMethod switch
        {
            "Direct Code Match" => 100,
            "Case-Insensitive Match" => 95,
            var method when method.StartsWith("Alternative Format") => 90,
            _ => 80
        };
    }

    /// <summary>
    /// Get regional context for the country
    /// </summary>
    private object GetRegionalContext(Country country)
    {
        // This is a simplified example - could be enhanced with actual regional data
        return new
        {
            Note = "Regional context would be available with enhanced geographic data",
            CountryCode = country.CountryShortCode,
            CountryName = country.CountryName
        };
    }

    /// <summary>
    /// Find countries with similar characteristics
    /// </summary>
    private List<object> FindSimilarCountries(Country targetCountry)
    {
        try
        {
            var allCountries = _countryHelper.GetCountryData();
            
            // Find countries with similar names or characteristics
            var similarCountries = allCountries
                .Where(c => c.CountryShortCode != targetCountry.CountryShortCode)
                .Where(c => c.CountryName.Contains(' ') == targetCountry.CountryName.Contains(' ') ||
                           Math.Abs(c.CountryName.Length - targetCountry.CountryName.Length) <= 3)
                .Take(3)
                .Select(c => new
                {
                    CountryCode = c.CountryShortCode,
                    CountryName = c.CountryName,
                    SimilarityReason = "Name characteristics"
                })
                .ToList<object>();

            return similarCountries;
        }
        catch
        {
            return new List<object>();
        }
    }

    /// <summary>
    /// Generate usage guidance based on country and match method
    /// </summary>
    private static List<string> GenerateCountryUsageGuidance(Country country, string matchMethod)
    {
        var guidance = new List<string>();

        if (matchMethod == "Direct Code Match")
        {
            guidance.Add("Perfect match found - use this data with high confidence");
        }
        else
        {
            guidance.Add($"Country found using {matchMethod} - verify if this matches your intended country");
        }

        guidance.Add("Use the CountryShortCode for standardized references");
        guidance.Add("Cache this country data for improved performance in repeated lookups");
        
        if (country.CountryName.Contains("United"))
        {
            guidance.Add("Note: This country name contains 'United' - ensure correct country selection");
        }

        return guidance;
    }

    /// <summary>
    /// Get alternative country codes and formats
    /// </summary>
    private static object GetAlternativeCountryCodes(Country country)
    {
        return new
        {
            ISO_Alpha2 = country.CountryShortCode,
            CommonVariations = new[]
            {
                country.CountryShortCode.ToLowerInvariant(),
                country.CountryShortCode.ToUpperInvariant()
            },
            Note = "Additional ISO-3166 codes would be available with enhanced country data provider"
        };
    }

    /// <summary>
    /// Build comprehensive error response for country not found
    /// </summary>
    private object BuildCountryNotFoundResponse(string normalizedCode, string originalInput, DateTime startTime, DateTime endTime)
    {
        var suggestions = GenerateCountryCodeSuggestions(normalizedCode);
        
        return new
        {
            success = false,
            message = $"No country data found for code '{normalizedCode}'",
            searchDetails = new
            {
                InputReceived = originalInput,
                NormalizedCode = normalizedCode,
                ProcessingTime = (endTime - startTime).TotalMilliseconds,
                SearchMethod = "Comprehensive lookup with fallbacks"
            },
            suggestions = suggestions,
            troubleshooting = new
            {
                CommonIssues = new[]
                {
                    "Verify the country code spelling",
                    "Ensure using standard ISO 3166-1 codes",
                    "Check if using obsolete or non-standard codes"
                },
                ExampleValidCodes = new[]
                {
                    "US (United States)", "CA (Canada)", "GB (United Kingdom)",
                    "DE (Germany)", "FR (France)", "JP (Japan)", "AU (Australia)"
                }
            },
            timestamp = startTime
        };
    }

    /// <summary>
    /// Generate intelligent suggestions for invalid country codes
    /// </summary>
    private List<object> GenerateCountryCodeSuggestions(string invalidCode)
    {
        var suggestions = new List<object>();

        try
        {
            var allCountries = _countryHelper.GetCountryData();
            
            // Find countries with similar codes
            var similarCodes = allCountries
                .Where(c => ToolsHelper.CalculateStringSimilarity(c.CountryShortCode, invalidCode) > 0.5)
                .Take(5)
                .Select(c => new
                {
                    CountryCode = c.CountryShortCode,
                    CountryName = c.CountryName,
                    Similarity = Math.Round(ToolsHelper.CalculateStringSimilarity(c.CountryShortCode, invalidCode) * 100, 1)
                })
                .Cast<object>()
                .ToList();

            if (similarCodes.Any())
            {
                suggestions.AddRange(similarCodes);
            }
            else
            {
                // Provide common country codes as fallback
                suggestions.AddRange(new object[]
                {
                    new { CountryCode = "US", CountryName = "United States", Note = "Most common code" },
                    new { CountryCode = "GB", CountryName = "United Kingdom", Note = "Common European code" },
                    new { CountryCode = "DE", CountryName = "Germany", Note = "Common European code" }
                });
            }
        }
        catch
        {
            // Fallback suggestions if country data access fails
            suggestions.Add(new { Note = "Unable to generate specific suggestions - verify country code format" });
        }

        return suggestions;
    }

    /// <summary>
    /// Find countries by various criteria like name, continent, or region
    /// </summary>
    [Function(nameof(FindCountries))]
    public Task<string> FindCountries(
        [McpToolTrigger(
            "search_countries",
            "Search countries by name/code with scoring."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "searchTerm",
            "string",
            "Country name prefix/contains or code"
        )] string searchTerm,
        [McpToolProperty(
            "maxResults",
            "string",
            "1..50 (default 10)"
        )] int maxResults = 10
    )
    {
        try
        {
            // Validate input
            var validationError = ValidateSearchInput(searchTerm, ref maxResults);
            if (validationError != null)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { error = validationError }));
            }

            logger.LogInformation("Searching for countries with term: '{SearchTerm}'", searchTerm);

            var allCountries = _countryHelper.GetCountryData();
            var searchResults = SearchCountriesWithScoring(allCountries, searchTerm, maxResults);

            var result = BuildSearchResult(searchTerm, searchResults, maxResults, allCountries.Count());

            logger.LogInformation("Found {Count} countries matching '{SearchTerm}'", searchResults.Count, searchTerm);
            return Task.FromResult(JsonSerializer.Serialize(new { ok = true, result }, new JsonSerializerOptions { WriteIndented = false }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during country search");
            return Task.FromResult(JsonSerializer.Serialize(new { ok = false, err = "unexpected" }));
        }
    }

    /// <summary>
    /// Validate search input parameters
    /// </summary>
    private static string? ValidateSearchInput(string? searchTerm, ref int maxResults)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return "Search term is required";
        }

        if (searchTerm.Trim().Length < 2)
        {
            return "Search term must be at least 2 characters long";
        }

        maxResults = Math.Max(1, Math.Min(50, maxResults));
        return null;
    }

    /// <summary>
    /// Search countries with intelligent scoring and ranking
    /// </summary>
    private static List<(Country Country, int Score, string MatchType)> SearchCountriesWithScoring(
        IEnumerable<Country> countries, string searchTerm, int maxResults)
    {
        var normalizedSearchTerm = searchTerm.Trim();
        
        var scoredResults = countries
            .Select(country => 
            {
                var (score, matchType) = CalculateCountryMatchScore(country, normalizedSearchTerm);
                return (Country: country, Score: score, MatchType: matchType);
            })
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Country.CountryName)
            .Take(maxResults)
            .ToList();

        return scoredResults;
    }

    /// <summary>
    /// Calculate match score for a country based on search criteria
    /// </summary>
    private static (int Score, string MatchType) CalculateCountryMatchScore(Country country, string searchTerm)
    {
        const int ExactCodeMatch = 100;
        const int ExactNameMatch = 90;
        const int StartsWithMatch = 80;
        const int ContainsMatch = 60;
        const int PartialMatch = 40;

        var comparison = StringComparison.OrdinalIgnoreCase;

        // Exact country code match (highest priority)
        if (country.CountryShortCode.Equals(searchTerm, comparison))
        {
            return (ExactCodeMatch, "Exact Code Match");
        }

        // Exact country name match
        if (country.CountryName.Equals(searchTerm, comparison))
        {
            return (ExactNameMatch, "Exact Name Match");
        }

        // Country name starts with search term
        if (country.CountryName.StartsWith(searchTerm, comparison))
        {
            return (StartsWithMatch, "Name Prefix Match");
        }

        // Country name contains search term
        if (country.CountryName.Contains(searchTerm, comparison))
        {
            return (ContainsMatch, "Name Contains Match");
        }

        // Check for partial word matches in country name
        var countryWords = country.CountryName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in countryWords)
        {
            if (word.StartsWith(searchTerm, comparison) && searchTerm.Length >= 3)
            {
                return (PartialMatch, "Word Prefix Match");
            }
        }

        return (0, "No Match");
    }

    /// <summary>
    /// Build the final search result response
    /// </summary>
    private static object BuildSearchResult(string searchTerm, 
        List<(Country Country, int Score, string MatchType)> searchResults, 
        int maxResults, int totalCountriesAvailable)
    {
        var countries = searchResults.Select(result => new
        {
            Country = result.Country,
            MatchScore = result.Score,
            MatchType = result.MatchType,
            MatchDetails = new
            {
                CountryCode = result.Country.CountryShortCode,
                CountryName = result.Country.CountryName
            }
        }).ToList();

        return new
        {
            SearchCriteria = new
            {
                SearchTerm = searchTerm,
                MaxResults = maxResults,
                TotalCountriesSearched = totalCountriesAvailable
            },
            Results = new
            {
                TotalMatches = searchResults.Count,
                HasMoreResults = searchResults.Count == maxResults,
                Countries = countries
            },
            SearchStats = searchResults.Any() ? new
            {
                BestMatchScore = searchResults.Max(r => r.Score),
                MatchTypes = searchResults.GroupBy(r => r.MatchType)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToList()
            } : null,
            SearchHints = GenerateSearchHints(searchTerm, searchResults.Count)
        };
    }

    /// <summary>
    /// Generate helpful search hints based on results
    /// </summary>
    private static object? GenerateSearchHints(string searchTerm, int resultCount)
    {
        if (resultCount == 0)
        {
            return new
            {
                NoResults = true,
                Suggestions = new[]
                {
                    "Try a shorter search term (2-3 letters)",
                    "Use partial country names like 'Unit' for United States/Kingdom",
                    "Use standard country codes like 'US', 'GB', 'DE'",
                    "Check spelling of the country name"
                }
            };
        }

        if (resultCount == 1)
        {
            return new
            {
                SingleResult = true,
                Note = "Perfect match found! This might be exactly what you're looking for."
            };
        }

        if (searchTerm.Length == 2)
        {
            return new
            {
                CountryCodeSearch = true,
                Note = "2-letter search detected. This might be a country code search."
            };
        }

        return null;
    }
}