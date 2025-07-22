using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Azure.Maps.Mcp.Services;

namespace Azure.Maps.Mcp.Tools;

public class SearchTool
{
    private readonly IAzureMapsService _azureMapsService;

    public SearchTool(IAzureMapsService azureMapsService)
    {
        _azureMapsService = azureMapsService;
    }

    [Function(nameof(Geocode))]
    public async Task<string> Geocode(
        [McpToolTrigger(
            "geocode",
            "Geocode a location, such as an address or landmark name using Azure Maps. It will also handle everything from exact street addresses or street or intersections as well as higher level geographies such as city centers, counties and states. The response also returns detailed address properties such as street, postal code, municipality, and country/region information.")] ToolInvocationContext context,
        [McpToolProperty(
            "location",
            "string",
            "address or landmark name")] string location
    )
    {
        var parameters = new Dictionary<string, string>
        {
            ["query"] = location
        };

        return await _azureMapsService.CallApiAsync("geocode", "2025-01-01", parameters);
    }
    
    [Function(nameof(ReverseGeocode))]
    public async Task<string> ReverseGeocode(
        [McpToolTrigger(
            "reverse_geocode",
            "Use to get a street address and location info from longitude and latitude coordinates.")] ToolInvocationContext context,
        [McpToolProperty(
            "longitude",
            "number",
            "Longitude defines the east-west position, measured in degrees from the Prime Meridian, which is 0° longitude.")] double longitude,
        [McpToolProperty(
            "latitude",
            "number",
            "Latitude specifies the north-south position of a point, measured in degrees from the Equator, which is 0° latitude.")] double latitude
    )
    {
        var parameters = new Dictionary<string, string>
        {
            ["coordinates"] = $"{longitude},{latitude}"
        };

        return await _azureMapsService.CallApiAsync("reverseGeocode", "2025-01-01", parameters);
    }
}