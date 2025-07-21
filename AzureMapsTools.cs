using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Configuration;

namespace Azure.Maps.Mcp;

public class AzureMapsTools(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    [Function(nameof(GeocodeLocation))]
    public async Task<string> GeocodeLocation(
        [McpToolTrigger("Geocode Location", "Geocode a location, such as an address or landmark name using Azure Maps")] ToolInvocationContext context
    )
    {
        // Get subscription key from configuration
        var subscriptionKey = configuration["AZURE_MAPS_SUBSCRIPTION_KEY"] 
            ?? throw new InvalidOperationException("Azure Maps subscription key not configured");

        // Get address from arguments
        var location = context.Arguments?["location"]?.ToString()?.Trim() 
            ?? throw new ArgumentException("Location parameter is required");

        // Build URL and call API
        var url = $"https://atlas.microsoft.com/geocode?api-version=2025-01-01&query={Uri.EscapeDataString(location)}&subscription-key={subscriptionKey}";
        
        using var httpClient = httpClientFactory.CreateClient("AzureMaps");
        var response = await httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Azure Maps API failed with status {response.StatusCode}: {error}");
        }

        var result = await response.Content.ReadAsStringAsync();
        
        return result;
    }
}