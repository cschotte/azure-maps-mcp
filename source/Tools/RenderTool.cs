// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Azure.Core.GeoJson;
using Azure.Maps.Mcp.Services;
using Azure.Maps.Mcp.Common;
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
public class RenderTool(IAzureMapsService azureMapsService)
{
    private readonly MapsRenderingClient _renderingClient = azureMapsService.RenderingClient;

    /// <summary>
    /// Generate a static map image with optional markers and paths
    /// </summary>
    [Function(nameof(GetStaticMapImage))]
    public async Task<string> GetStaticMapImage(
        [McpToolTrigger(
            "render_staticmap",
            "Generate a static map image for a geographic area with optional markers and paths. Returns high-quality PNG images."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "boundingBox",
            "string",
            "JSON defining map area. Format: '{\"west\": -122.4, \"south\": 47.5, \"east\": -122.2, \"north\": 47.7}'"
        )] string boundingBox = "{\"west\": -122.4, \"south\": 47.5, \"east\": -122.2, \"north\": 47.7}",
        [McpToolProperty(
            "zoomLevel",
            "number",
            "Zoom level (1-20). Higher = more detail. Example: 10"
        )] int zoomLevel = 10,
        [McpToolProperty(
            "width",
            "number",
            "Image width in pixels (1-8192). Example: 512"
        )] int width = 512,
        [McpToolProperty(
            "height",
            "number",
            "Image height in pixels (1-8192). Example: 512"
        )] int height = 512,
        [McpToolProperty(
            "mapStyle",
            "string",
            "Map style: 'road', 'satellite', 'hybrid'. Default: 'road'"
        )] string mapStyle = "road",
        [McpToolProperty(
            "markers",
            "array",
            "Optional markers: [{'latitude': 47.6, 'longitude': -122.3, 'label': 'Seattle', 'color': 'red'}]"
        )] MarkerInfo[]? markers = null,
        [McpToolProperty(
            "paths",
            "array", 
            "Optional paths: [{'coordinates': [{'latitude': 47.6, 'longitude': -122.3}], 'color': 'blue', 'width': 3}]"
        )] PathInfo[]? paths = null
    )
    {
        try
        {
            // Parse and validate bounding box
            if (!TryParseBoundingBox(boundingBox, out var parsedBbox, out var bboxError))
                return ResponseHelper.CreateErrorResponse(bboxError!);
            var geoBoundingBox = parsedBbox!;

            // Validate parameters
            var zoomValidation = ValidationHelper.ValidateRange(zoomLevel, 1, 20, "zoom level");
            if (!zoomValidation.IsValid)
                return ResponseHelper.CreateErrorResponse(zoomValidation.ErrorMessage!);

            var widthValidation = ValidationHelper.ValidateRange(width, 1, 8192, "width");
            if (!widthValidation.IsValid)
                return ResponseHelper.CreateErrorResponse(widthValidation.ErrorMessage!);

            var heightValidation = ValidationHelper.ValidateRange(height, 1, 8192, "height");
            if (!heightValidation.IsValid)
                return ResponseHelper.CreateErrorResponse(heightValidation.ErrorMessage!);

            var validStyles = new HashSet<string>(new[] { "road", "satellite", "hybrid" }, StringComparer.OrdinalIgnoreCase);
            if (!validStyles.Contains(mapStyle))
                return ResponseHelper.CreateErrorResponse($"Map style must be one of: {string.Join(", ", validStyles)}");
            
            var pushpinStyles = new List<ImagePushpinStyle>();
            var pathStyles = new List<ImagePathStyle>();

            // Process markers if provided
            if (markers?.Length > 0)
            {
                var pushpinPositions = markers
                    .Select(m => new PushpinPosition(m.Longitude, m.Latitude, m.Label ?? string.Empty))
                    .ToList();

                if (pushpinPositions.Count > 0)
                    pushpinStyles.Add(new ImagePushpinStyle(pushpinPositions)
                    {
                        PushpinScaleRatio = 1.0,
                        LabelScaleRatio = 18
                    });
            }

            // Process paths if provided
            if (paths?.Length > 0)
            {
                foreach (var path in paths)
                {
                    if (path.Coordinates is { Length: >= 2 })
                    {
                        var geoPositions = path.Coordinates.Select(c => new GeoPosition(c.Longitude, c.Latitude)).ToList();
                        pathStyles.Add(new ImagePathStyle(geoPositions)
                        {
                            LineWidthInPixels = path.Width
                        });
                    }
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
                    image_data_uri = $"data:image/png;base64,{base64Image}",
                    size_bytes = imageBytes.Length,
                    dimensions = new { width, height },
                    zoom_level = zoomLevel,
                    markers_count = markers?.Length ?? 0,
                    paths_count = paths?.Length ?? 0
                };

                return ResponseHelper.CreateSuccessResponse(result);
            }

            return ResponseHelper.CreateErrorResponse("No image data returned from Azure Maps");
        }
        catch (RequestFailedException ex)
        {
            return ResponseHelper.CreateErrorResponse($"Azure Maps API error: {ex.Message}");
        }
        catch (Exception)
        {
            return ResponseHelper.CreateErrorResponse("Failed to generate static map image");
        }
    }

    private static bool TryParseBoundingBox(string json, out GeoBoundingBox? bbox, out string? error)
    {
        bbox = null;
        error = null;
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, double>>(json);
            if (dict == null || !dict.TryGetValue("west", out var west) ||
                !dict.TryGetValue("south", out var south) ||
                !dict.TryGetValue("east", out var east) ||
                !dict.TryGetValue("north", out var north))
            {
                error = "Bounding box must contain 'west', 'south', 'east', and 'north' properties";
                return false;
            }

            bbox = new GeoBoundingBox(west, south, east, north);
            return true;
        }
        catch (JsonException)
        {
            error = "Invalid bounding box JSON format";
            return false;
        }
    }
}