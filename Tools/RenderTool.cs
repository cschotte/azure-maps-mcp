using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Azure.Maps.Mcp.Services;

namespace Azure.Maps.Mcp.Tools;

public class RenderTool
{
    private readonly IAzureMapsService _azureMapsService;

    public RenderTool(IAzureMapsService azureMapsService)
    {
        _azureMapsService = azureMapsService;
    }

    [Function(nameof(StaticMap))]
    public async Task<object> StaticMap(
        [McpToolTrigger(
            "staticmap",
            "Get static map image with custom pins and labels.")] ToolInvocationContext context,
        [McpToolProperty(
            "longitude",
            "number",
            "Longitude defines the east-west position, measured in degrees from the Prime Meridian, which is 0° longitude.")] double longitude,
        [McpToolProperty(
            "latitude",
            "number",
            "Latitude specifies the north-south position of a point, measured in degrees from the Equator, which is 0° latitude.")] double latitude,
        [McpToolProperty(
            "zoom",
            "number",
            "Zoom level for the map (1-22). Higher values show more detail. Default is 13 for city-level view.")] double zoom = 13
    )
    {
        var parameters = new Dictionary<string, string>
        {
            // Example static map with pins and path
            // https://atlas.microsoft.com/map/static?subscription-key={Your-Azure-Maps-Subscription-key}&zoom=13&tilesetId=microsoft.base.road&api-version=2024-04-01&language=en-us&center=-73.964085, 40.78477&path=lcFF0000|lw2|la0.60|ra700||-122.13230609893799 47.64599069048016&pins=custom%7Cla15+50%7Cls12%7Clc003b61%7C%7C%27Central Park%27-73.9657974+40.781971%7C%7Chttps%3A%2F%2Fsamples.azuremaps.com%2Fimages%2Ficons%2Fylw-pushpin.png
            ["zoom"] = zoom.ToString(),
            ["tilesetId"] = "microsoft.base.road",
            ["language"] = "en-us",
            ["center"] = $"{longitude}, {latitude}",
            ["path"] = $"lcFF0000|lw2|la0.60|ra700||{longitude} {latitude}",
            ["pins"] = $"custom%7Cla15+50%7Cls12%7Clc003b61%7C%7C%27Central Park%27{longitude}+{latitude}%7C%7Chttps%3A%2F%2Fsamples.azuremaps.com%2Fimages%2Ficons%2Fylw-pushpin.png"
        };

        var imageData = await _azureMapsService.CallImageApiAsync("map/static", "2024-04-01", parameters);
        
        return new
        {
            type = "image",
            data = Convert.ToBase64String(imageData.Data),
            mimeType = imageData.ContentType
        };
    }

}