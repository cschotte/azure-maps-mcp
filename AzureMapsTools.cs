using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Configuration;

namespace Azure.Maps.Mcp;

public class AzureMapsTools(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    [Function(nameof(GeocodeLocation))]
    public async Task<string> GeocodeLocation(
        // This attribute registers the function as an MCP tool trigger.
        // The name and description help users understand what the tool does.
        [McpToolTrigger(
            "geocode_location",
            "Geocode a location, such as an address or landmark name using Azure Maps")] ToolInvocationContext context,
        [McpToolProperty(
            "location",
            "string",
            "address or landmark name")] string location 
    )
    {
        // Get the Azure Maps subscription key from configuration.
        // This key is needed to authenticate requests to the Azure Maps API.
        // If the key is missing, throw an error to alert the developer.
        var subscriptionKey = configuration["AZURE_MAPS_SUBSCRIPTION_KEY"] 
            ?? throw new InvalidOperationException("Azure Maps subscription key not configured");

        // Build the URL for the Azure Maps geocoding API.
        // The query parameter contains the location, and the subscription key authenticates the request.
        var url = $"https://atlas.microsoft.com/geocode?api-version=2025-01-01&query={Uri.EscapeDataString(location)}&subscription-key={subscriptionKey}";

        // Create an HTTP client to send the request.
        using var httpClient = httpClientFactory.CreateClient("AzureMaps");

        // Send the GET request to the Azure Maps API.
        var response = await httpClient.GetAsync(url);

        // Check if the response was successful.
        // If not, read the error message and throw an exception.
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Azure Maps API failed with status {response.StatusCode}: {error}");
        }

        // Read the result from the response as a string (JSON format).
        var result = await response.Content.ReadAsStringAsync();

        // Return the geocoding result to the caller.
        return result;
    }
}