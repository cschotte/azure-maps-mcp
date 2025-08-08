// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using Azure.Core.GeoJson;
using Azure.Maps.Mcp.Services;
using Azure.Maps.Rendering;
using System.Text.Json;

namespace Azure.Maps.Mcp.Tools;

/// <summary>
/// Represents a marker to be placed on the map
/// </summary>
public class MarkerInfo
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Label { get; set; }
    public string? Color { get; set; }
}

/// <summary>
/// Represents coordinate information for map rendering paths
/// </summary>
public class MapCoordinateInfo
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

/// <summary>
/// Represents a path to be drawn on the map
/// </summary>
public class PathInfo
{
    public MapCoordinateInfo[]? Coordinates { get; set; }
    public string? Color { get; set; }
    public int Width { get; set; } = 3;
}

/// <summary>
/// Azure Maps Rendering Tool providing map rendering capabilities
/// </summary>
public class RenderTool(IAzureMapsService azureMapsService, ILogger<RenderTool> logger)
{
    private readonly MapsRenderingClient _renderingClient = azureMapsService.RenderingClient;

    /// <summary>
    /// Generate a static map image with optional markers and paths
    /// </summary>
    [Function(nameof(GetStaticMapImage))]
    public async Task<string> GetStaticMapImage(
        [McpToolTrigger(
            "render_staticmap",
            "Generate a custom static map image for a specified geographic area with optional markers and path overlays. This service creates publication-ready map images perfect for reports, documentation, presentations, or embedding in applications. Supports various map styles, custom markers for points of interest, and path drawing for routes or boundaries. Returns high-quality PNG images that can be directly used or embedded."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "boundingBox",
            "string",
            "JSON object defining the rectangular map area to display. Must contain west, south, east, north coordinates. Format: '{\"west\": -122.4, \"south\": 47.5, \"east\": -122.2, \"north\": 47.7}'. Coordinates define the viewing rectangle."
        )] string boundingBox = "{\"west\": -122.4, \"south\": 47.5, \"east\": -122.2, \"north\": 47.7}",
        [McpToolProperty(
            "zoomLevel",
            "number",
            "Zoom level for the map as a number (e.g., 10). Must be between 1 and 20. Higher numbers show more detail. Examples: 8 (city level), 12 (neighborhood level), 16 (street level)"
        )] int zoomLevel = 10,
        [McpToolProperty(
            "width",
            "number",
            "Width of the image in pixels (e.g., 512). Must be between 1 and 8192. Examples: 256, 512, 1024"
        )] int width = 512,
        [McpToolProperty(
            "height",
            "number",
            "Height of the image in pixels (e.g., 512). Must be between 1 and 8192. Examples: 256, 512, 1024"
        )] int height = 512,
        [McpToolProperty(
            "mapStyle",
            "string",
            "Visual style of the map: 'road' (street map with roads, default), 'satellite' (aerial/satellite imagery), 'hybrid' (satellite with road overlay). Examples: 'road', 'satellite', 'hybrid'"
        )] string mapStyle = "road",
        [McpToolProperty(
            "markers",
            "array",
            "Optional array of marker objects to place on the map. Each marker should have latitude, longitude, and optional label and color properties. Leave empty or null for no markers. Example: [{'latitude': 47.6062, 'longitude': -122.3321, 'label': 'Seattle', 'color': 'red'}]"
        )] MarkerInfo[]? markers = null,
        [McpToolProperty(
            "paths",
            "array", 
            "Optional array of path objects to draw lines/routes on the map. Each path should have coordinates array and optional color and width properties. Leave empty or null for no paths. Example: [{'coordinates': [{'latitude': 47.6062, 'longitude': -122.3321}, {'latitude': 47.6205, 'longitude': -122.3493}], 'color': 'blue', 'width': 3}]"
        )] PathInfo[]? paths = null
    )
    {
        try
        {
            Dictionary<string, double>? bbox;
            try
            {
                bbox = JsonSerializer.Deserialize<Dictionary<string, double>>(boundingBox);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to parse bounding box JSON: {BoundingBox}", boundingBox);
                return JsonSerializer.Serialize(new { error = $"Invalid bounding box JSON format. Expected: {{\"west\": -122.4, \"south\": 47.5, \"east\": -122.2, \"north\": 47.7}}. Error: {ex.Message}" });
            }
            
            if (bbox == null || !bbox.ContainsKey("west") || !bbox.ContainsKey("south") || 
                !bbox.ContainsKey("east") || !bbox.ContainsKey("north"))
            {
                return JsonSerializer.Serialize(new { error = "Bounding box must contain 'west', 'south', 'east', and 'north' properties" });
            }

            if (zoomLevel < 1 || zoomLevel > 20)
            {
                return JsonSerializer.Serialize(new { error = "Zoom level must be between 1 and 20" });
            }

            if (width < 1 || width > 8192 || height < 1 || height > 8192)
            {
                return JsonSerializer.Serialize(new { error = "Width and height must be between 1 and 8192 pixels" });
            }

            logger.LogInformation("Generating static map image with bounds: {BoundingBox}", boundingBox);

            var geoBoundingBox = new GeoBoundingBox(bbox["west"], bbox["south"], bbox["east"], bbox["north"]);
            
            var pushpinStyles = new List<ImagePushpinStyle>();
            var pathStyles = new List<ImagePathStyle>();

            // Parse markers if provided
            if (markers != null && markers.Length > 0)
            {
                try
                {
                    var pushpinPositions = new List<PushpinPosition>();
                    foreach (var marker in markers)
                    {
                        var label = marker.Label ?? "";
                        pushpinPositions.Add(new PushpinPosition(marker.Longitude, marker.Latitude, label));
                    }
                    
                    if (pushpinPositions.Any())
                    {
                        var pushpinStyle = new ImagePushpinStyle(pushpinPositions)
                        {
                            PushpinScaleRatio = 1.0,
                            LabelScaleRatio = 18
                        };
                        pushpinStyles.Add(pushpinStyle);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to process markers, continuing without markers");
                }
            }

            // Parse paths if provided
            if (paths != null && paths.Length > 0)
            {
                try
                {
                    foreach (var path in paths)
                    {
                        if (path.Coordinates != null && path.Coordinates.Length >= 2)
                        {
                            var geoPositions = path.Coordinates.Select(c => new GeoPosition(c.Longitude, c.Latitude)).ToList();
                            var pathStyle = new ImagePathStyle(geoPositions)
                            {
                                LineWidthInPixels = path.Width
                            };
                            pathStyles.Add(pathStyle);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to process paths, continuing without paths");
                }
            }

            var staticImageOptions = new GetMapStaticImageOptions(geoBoundingBox, pushpinStyles, pathStyles)
            {
                ZoomLevel = zoomLevel,
                Language = RenderingLanguage.EnglishUsa
            };

            var response = await _renderingClient.GetMapStaticImageAsync(staticImageOptions);

            if (response.Value != null)
            {
                using var memoryStream = new MemoryStream();
                await response.Value.CopyToAsync(memoryStream);
                var imageBytes = memoryStream.ToArray();
                var base64Image = Convert.ToBase64String(imageBytes);

                var result = new
                {
                    MapInfo = new
                    {
                        BoundingBox = new
                        {
                            West = bbox["west"],
                            South = bbox["south"],
                            East = bbox["east"],
                            North = bbox["north"]
                        },
                        ZoomLevel = zoomLevel,
                        Dimensions = new { Width = width, Height = height },
                        Style = mapStyle,
                        MarkerCount = markers?.Length ?? 0,
                        PathCount = paths?.Length ?? 0
                    },
                    ImageData = new
                    {
                        Format = "PNG",
                        //Base64Data = base64Image,
                        SizeInBytes = imageBytes.Length,
                        DataUri = $"data:image/png;base64,{base64Image}"
                    }
                };

                logger.LogInformation("Successfully generated static map image: {Width}x{Height}, {SizeKB}KB", 
                    width, height, Math.Round(imageBytes.Length / 1024.0, 2));
                return JsonSerializer.Serialize(new { success = true, result }, new JsonSerializerOptions { WriteIndented = false });
            }

            logger.LogWarning("No static map image data returned");
            return JsonSerializer.Serialize(new { success = false, message = "No static map image data returned" });
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Maps API error during static image generation: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = $"API Error: {ex.Message}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during static image generation");
            return JsonSerializer.Serialize(new { error = "An unexpected error occurred" });
        }
    }
}