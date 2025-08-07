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
/// Azure Maps Rendering Tool providing map rendering capabilities including tiles, static images, and tile metadata
/// </summary>
public class RenderTool(IAzureMapsService azureMapsService, ILogger<RenderTool> logger)
{
    private readonly MapsRenderingClient _renderingClient = azureMapsService.RenderingClient;

    /// <summary>
    /// Get a map tile image for specific coordinates and zoom level
    /// </summary>
    [Function(nameof(GetMapTile))]
    public async Task<string> GetMapTile(
        [McpToolTrigger(
            "get_map_tile",
            "Retrieve a map tile image for specific geographic coordinates and zoom level. Map tiles are the building blocks of web maps, providing visual representation of geographic areas. This service returns high-quality map tiles in PNG format with various styles (road maps, satellite imagery, hybrid views). Essential for building custom mapping applications, creating map overlays, and displaying geographic context."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "latitude",
            "string",
            "Latitude coordinate for the tile center as a decimal number (e.g., '47.6062'). Must be between -90 and 90 degrees."
        )] double latitude,
        [McpToolProperty(
            "longitude",
            "string",
            "Longitude coordinate for the tile center as a decimal number (e.g., '-122.3321'). Must be between -180 and 180 degrees."
        )] double longitude,
        [McpToolProperty(
            "zoomLevel",
            "string",
            "Zoom level for the tile as a string number (e.g., '10'). Must be between 1 and 22. Higher numbers show more detail but smaller area."
        )] int zoomLevel = 10,
        [McpToolProperty(
            "tileSetId",
            "string",
            "The tile set style to use: 'microsoft.base.road' (street map with roads, default), 'microsoft.base.hybrid' (satellite imagery with road labels), 'microsoft.imagery' (pure satellite imagery)."
        )] string tileSetId = "microsoft.base.road",
        [McpToolProperty(
            "tileSize",
            "string",
            "Size of the tile in pixels as a string number (e.g., '256' or '512'). Only 256 and 512 are supported. Default is '256'."
        )] int tileSize = 256
    )
    {
        try
        {
            if (latitude < -90 || latitude > 90)
            {
                return JsonSerializer.Serialize(new { error = "Latitude must be between -90 and 90 degrees" });
            }

            if (longitude < -180 || longitude > 180)
            {
                return JsonSerializer.Serialize(new { error = "Longitude must be between -180 and 180 degrees" });
            }

            if (zoomLevel < 1 || zoomLevel > 22)
            {
                return JsonSerializer.Serialize(new { error = "Zoom level must be between 1 and 22" });
            }

            if (tileSize != 256 && tileSize != 512)
            {
                return JsonSerializer.Serialize(new { error = "Tile size must be either 256 or 512 pixels" });
            }

            logger.LogInformation("Getting map tile for coordinates: {Latitude}, {Longitude} at zoom {ZoomLevel}", latitude, longitude, zoomLevel);

            // Validate tile set ID options
            var validTileSetIds = new Dictionary<string, MapTileSetId>(StringComparer.OrdinalIgnoreCase)
            {
                { "microsoft.base.road", MapTileSetId.MicrosoftBaseRoad },
                { "microsoft.base.hybrid", MapTileSetId.MicrosoftBaseHybrid },
                { "microsoft.imagery", MapTileSetId.MicrosoftImagery }
            };

            if (!validTileSetIds.TryGetValue(tileSetId, out var parsedTileSetId))
            {
                var validOptions = string.Join(", ", validTileSetIds.Keys);
                return JsonSerializer.Serialize(new { error = $"Invalid tile set ID '{tileSetId}'. Valid options: {validOptions}" });
            }

            // Calculate tile index from coordinates
            var tileIndex = MapsRenderingClient.PositionToTileXY(new GeoPosition(longitude, latitude), zoomLevel, tileSize);

            var options = new GetMapTileOptions(parsedTileSetId, new MapTileIndex(tileIndex.X, tileIndex.Y, zoomLevel));

            var response = await _renderingClient.GetMapTileAsync(options);

            if (response.Value != null)
            {
                using var memoryStream = new MemoryStream();
                await response.Value.CopyToAsync(memoryStream);
                var imageBytes = memoryStream.ToArray();
                var base64Image = Convert.ToBase64String(imageBytes);

                var result = new
                {
                    TileInfo = new
                    {
                        Coordinates = new { Latitude = latitude, Longitude = longitude },
                        ZoomLevel = zoomLevel,
                        TileIndex = new { X = tileIndex.X, Y = tileIndex.Y },
                        TileSize = tileSize,
                        TileSetId = tileSetId
                    },
                    ImageData = new
                    {
                        Format = "PNG",
                        Base64Data = base64Image,
                        SizeInBytes = imageBytes.Length,
                        DataUri = $"data:image/png;base64,{base64Image}"
                    }
                };

                logger.LogInformation("Successfully retrieved map tile: {SizeKB}KB", Math.Round(imageBytes.Length / 1024.0, 2));
                return JsonSerializer.Serialize(new { success = true, result }, new JsonSerializerOptions { WriteIndented = true });
            }

            logger.LogWarning("No map tile data returned");
            return JsonSerializer.Serialize(new { success = false, message = "No map tile data returned" });
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Maps API error during map tile retrieval: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = $"API Error: {ex.Message}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during map tile retrieval");
            return JsonSerializer.Serialize(new { error = "An unexpected error occurred" });
        }
    }

    /// <summary>
    /// Generate a static map image with optional markers and paths
    /// </summary>
    [Function(nameof(GetStaticMapImage))]
    public async Task<string> GetStaticMapImage(
        [McpToolTrigger(
            "get_static_map_image",
            "Generate a custom static map image for a specified geographic area with optional markers and path overlays. This service creates publication-ready map images perfect for reports, documentation, presentations, or embedding in applications. Supports various map styles, custom markers for points of interest, and path drawing for routes or boundaries. Returns high-quality PNG images that can be directly used or embedded."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "boundingBox",
            "string",
            "JSON object defining the rectangular map area to display. Must contain west, south, east, north coordinates. Format: '{\"west\": -122.4, \"south\": 47.5, \"east\": -122.2, \"north\": 47.7}'. Coordinates define the viewing rectangle."
        )] string boundingBox = "{\"west\": -122.4, \"south\": 47.5, \"east\": -122.2, \"north\": 47.7}",
        [McpToolProperty(
            "zoomLevel",
            "string",
            "Zoom level for the map as a string number (e.g., '10'). Must be between 1 and 20. Higher numbers show more detail."
        )] int zoomLevel = 10,
        [McpToolProperty(
            "width",
            "string",
            "Width of the image in pixels as a string number (e.g., '512'). Must be between 1 and 8192."
        )] int width = 512,
        [McpToolProperty(
            "height",
            "string",
            "Height of the image in pixels as a string number (e.g., '512'). Must be between 1 and 8192."
        )] int height = 512,
        [McpToolProperty(
            "mapStyle",
            "string",
            "Visual style of the map: 'road' (street map with roads, default), 'satellite' (aerial/satellite imagery), 'hybrid' (satellite with road overlay)."
        )] string mapStyle = "road",
        [McpToolProperty(
            "markers",
            "string",
            "Optional JSON array of marker objects to place on the map. Each marker should have latitude, longitude, and optional label and color. Format: '[{\"latitude\": 47.6, \"longitude\": -122.3, \"label\": \"Marker 1\", \"color\": \"red\"}]'. Leave empty or null for no markers."
        )] string? markers = null,
        [McpToolProperty(
            "paths",
            "string",
            "Optional JSON array of path objects to draw lines/routes on the map. Each path should have coordinates array and optional color and width. Format: '[{\"coordinates\": [{\"latitude\": 47.6, \"longitude\": -122.3}, {\"latitude\": 47.7, \"longitude\": -122.2}], \"color\": \"blue\", \"width\": 3}]'. Leave empty or null for no paths."
        )] string? paths = null
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
            if (!string.IsNullOrWhiteSpace(markers))
            {
                try
                {
                    var markerList = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(markers);
                    if (markerList != null)
                    {
                        var pushpinPositions = new List<PushpinPosition>();
                        foreach (var marker in markerList)
                        {
                            if (marker.ContainsKey("latitude") && marker.ContainsKey("longitude"))
                            {
                                var lat = marker["latitude"].GetDouble();
                                var lon = marker["longitude"].GetDouble();
                                var label = marker.ContainsKey("label") ? marker["label"].GetString() ?? "" : "";
                                pushpinPositions.Add(new PushpinPosition(lon, lat, label));
                            }
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
                }
                catch (JsonException)
                {
                    logger.LogWarning("Failed to parse markers JSON, continuing without markers");
                }
            }

            // Parse paths if provided
            if (!string.IsNullOrWhiteSpace(paths))
            {
                try
                {
                    var pathList = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(paths);
                    if (pathList != null)
                    {
                        foreach (var path in pathList)
                        {
                            if (path.ContainsKey("coordinates"))
                            {
                                var coordsJson = JsonSerializer.Serialize(path["coordinates"]);
                                var coords = JsonSerializer.Deserialize<List<Dictionary<string, double>>>(coordsJson);
                                if (coords != null && coords.Count >= 2)
                                {
                                    var geoPositions = coords.Select(c => new GeoPosition(c["longitude"], c["latitude"])).ToList();
                                    var pathStyle = new ImagePathStyle(geoPositions)
                                    {
                                        LineWidthInPixels = path.ContainsKey("width") ? path["width"].GetInt32() : 3
                                    };
                                    pathStyles.Add(pathStyle);
                                }
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    logger.LogWarning("Failed to parse paths JSON, continuing without paths");
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
                        MarkerCount = pushpinStyles.Sum(p => p.PushpinPositions?.Count ?? 0),
                        PathCount = pathStyles.Count
                    },
                    ImageData = new
                    {
                        Format = "PNG",
                        Base64Data = base64Image,
                        SizeInBytes = imageBytes.Length,
                        DataUri = $"data:image/png;base64,{base64Image}"
                    }
                };

                logger.LogInformation("Successfully generated static map image: {Width}x{Height}, {SizeKB}KB", 
                    width, height, Math.Round(imageBytes.Length / 1024.0, 2));
                return JsonSerializer.Serialize(new { success = true, result }, new JsonSerializerOptions { WriteIndented = true });
            }

            logger.LogWarning("No static map image data returned");
            return JsonSerializer.Serialize(new { success = false, message = "No static map image data returned" });
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid JSON format in request parameters");
            return JsonSerializer.Serialize(new { error = "Invalid JSON format in request parameters" });
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