// Copyright (c) 2025 Clemens Schotte
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

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
    /// Get map tile metadata for a specific tile set
    /// </summary>
    [Function(nameof(GetMapTileSetMetadata))]
    public async Task<string> GetMapTileSetMetadata(
        [McpToolTrigger(
            "get_map_tileset_metadata",
            "Get metadata information for a specific map tile set, including tile scheme, endpoints, and supported zoom levels."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "tileSetId",
            "string",
            "The tile set ID to get metadata for. Options: 'microsoft.base.road', 'microsoft.base.hybrid', 'microsoft.imagery', 'microsoft.weather.radar.main', 'microsoft.weather.infrared.main'"
        )] string tileSetId = "microsoft.base.road"
    )
    {
        try
        {
            logger.LogInformation("Getting tile set metadata for: {TileSetId}", tileSetId);

            // Parse tile set ID
            if (!Enum.TryParse<MapTileSetId>(tileSetId.Replace(".", ""), true, out var parsedTileSetId))
            {
                return JsonSerializer.Serialize(new { error = $"Invalid tile set ID '{tileSetId}'. Valid options: microsoft.base.road, microsoft.base.hybrid, microsoft.imagery, microsoft.weather.radar.main, microsoft.weather.infrared.main" });
            }

            var response = await _renderingClient.GetMapTileSetAsync(parsedTileSetId);

            if (response.Value != null)
            {
                var tileSet = response.Value;
                var result = new
                {
                    TileSetName = tileSet.TileSetName,
                    TileScheme = tileSet.TileScheme.ToString(),
                    TileEndpoints = tileSet.TileEndpoints?.ToList()
                };

                logger.LogInformation("Successfully retrieved tile set metadata for {TileSetName}", result.TileSetName);
                return JsonSerializer.Serialize(new { success = true, result }, new JsonSerializerOptions { WriteIndented = true });
            }

            logger.LogWarning("No tile set metadata found for: {TileSetId}", tileSetId);
            return JsonSerializer.Serialize(new { success = false, message = "No tile set metadata found" });
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Maps API error during tile set metadata retrieval: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = $"API Error: {ex.Message}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during tile set metadata retrieval");
            return JsonSerializer.Serialize(new { error = "An unexpected error occurred" });
        }
    }

    /// <summary>
    /// Get a map tile image for specific coordinates and zoom level
    /// </summary>
    [Function(nameof(GetMapTile))]
    public async Task<string> GetMapTile(
        [McpToolTrigger(
            "get_map_tile",
            "Get a map tile image for specific coordinates and zoom level. Returns the tile as a base64-encoded PNG image."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "latitude",
            "number",
            "Latitude coordinate for the tile center (e.g., 47.6062)"
        )] double latitude,
        [McpToolProperty(
            "longitude",
            "number",
            "Longitude coordinate for the tile center (e.g., -122.3321)"
        )] double longitude,
        [McpToolProperty(
            "zoomLevel",
            "number",
            "Zoom level for the tile (1-22, default: 10)"
        )] int zoomLevel = 10,
        [McpToolProperty(
            "tileSetId",
            "string",
            "The tile set style. Options: 'microsoft.base.road' (default), 'microsoft.base.hybrid', 'microsoft.imagery'"
        )] string tileSetId = "microsoft.base.road",
        [McpToolProperty(
            "tileSize",
            "number",
            "Size of the tile in pixels (256 or 512, default: 256)"
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

            // Parse tile set ID
            if (!Enum.TryParse<MapTileSetId>(tileSetId.Replace(".", ""), true, out var parsedTileSetId))
            {
                return JsonSerializer.Serialize(new { error = $"Invalid tile set ID '{tileSetId}'. Valid options: microsoft.base.road, microsoft.base.hybrid, microsoft.imagery" });
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
            "Generate a static map image for a specified area with optional markers and paths. Returns the image as a base64-encoded PNG."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "boundingBox",
            "object",
            "Bounding box defining the map area. Format: {\"west\": -122.4, \"south\": 47.5, \"east\": -122.2, \"north\": 47.7}"
        )] string boundingBox,
        [McpToolProperty(
            "zoomLevel",
            "number",
            "Zoom level for the map (1-20, default: 10)"
        )] int zoomLevel = 10,
        [McpToolProperty(
            "width",
            "number",
            "Width of the image in pixels (1-8192, default: 512)"
        )] int width = 512,
        [McpToolProperty(
            "height",
            "number",
            "Height of the image in pixels (1-8192, default: 512)"
        )] int height = 512,
        [McpToolProperty(
            "mapStyle",
            "string",
            "Map style. Options: 'road' (default), 'satellite', 'hybrid'"
        )] string mapStyle = "road",
        [McpToolProperty(
            "markers",
            "array",
            "Optional markers to add to the map. Format: [{\"latitude\": 47.6, \"longitude\": -122.3, \"label\": \"Marker 1\", \"color\": \"red\"}]"
        )] string? markers = null,
        [McpToolProperty(
            "paths",
            "array",
            "Optional paths to draw on the map. Format: [{\"coordinates\": [{\"latitude\": 47.6, \"longitude\": -122.3}, {\"latitude\": 47.7, \"longitude\": -122.2}], \"color\": \"blue\", \"width\": 3}]"
        )] string? paths = null
    )
    {
        try
        {
            var bbox = JsonSerializer.Deserialize<Dictionary<string, double>>(boundingBox);
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
                    var markerList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(markers);
                    if (markerList != null)
                    {
                        var pushpinPositions = new List<PushpinPosition>();
                        foreach (var marker in markerList)
                        {
                            if (marker.ContainsKey("latitude") && marker.ContainsKey("longitude"))
                            {
                                var lat = Convert.ToDouble(marker["latitude"]);
                                var lon = Convert.ToDouble(marker["longitude"]);
                                var label = marker.ContainsKey("label") ? marker["label"].ToString() : "";
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
                    var pathList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(paths);
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
                                        LineWidthInPixels = path.ContainsKey("width") ? Convert.ToInt32(path["width"]) : 3
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

    /// <summary>
    /// Calculate tile coordinates for a given position and zoom level
    /// </summary>
    [Function(nameof(GetTileCoordinates))]
    public Task<string> GetTileCoordinates(
        [McpToolTrigger(
            "get_tile_coordinates",
            "Calculate tile X, Y coordinates and other tile information for a given geographic position and zoom level."
        )] ToolInvocationContext context,
        [McpToolProperty(
            "latitude",
            "number",
            "Latitude coordinate (e.g., 47.6062)"
        )] double latitude,
        [McpToolProperty(
            "longitude",
            "number",
            "Longitude coordinate (e.g., -122.3321)"
        )] double longitude,
        [McpToolProperty(
            "zoomLevel",
            "number",
            "Zoom level (1-22, default: 10)"
        )] int zoomLevel = 10,
        [McpToolProperty(
            "tileSize",
            "number",
            "Size of the tile in pixels (256 or 512, default: 256)"
        )] int tileSize = 256
    )
    {
        try
        {
            if (latitude < -90 || latitude > 90)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { error = "Latitude must be between -90 and 90 degrees" }));
            }

            if (longitude < -180 || longitude > 180)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { error = "Longitude must be between -180 and 180 degrees" }));
            }

            if (zoomLevel < 1 || zoomLevel > 22)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { error = "Zoom level must be between 1 and 22" }));
            }

            if (tileSize != 256 && tileSize != 512)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { error = "Tile size must be either 256 or 512 pixels" }));
            }

            logger.LogInformation("Calculating tile coordinates for: {Latitude}, {Longitude} at zoom {ZoomLevel}", latitude, longitude, zoomLevel);

            // Calculate tile index from coordinates
            var tileIndex = MapsRenderingClient.PositionToTileXY(new GeoPosition(longitude, latitude), zoomLevel, tileSize);

            // Calculate some additional useful information
            var totalTilesAtZoom = Math.Pow(2, zoomLevel);
            var tileWorldSize = tileSize * totalTilesAtZoom;

            var result = new
            {
                InputCoordinates = new
                {
                    Latitude = latitude,
                    Longitude = longitude
                },
                TileCoordinates = new
                {
                    X = tileIndex.X,
                    Y = tileIndex.Y,
                    ZoomLevel = zoomLevel
                },
                TileInfo = new
                {
                    TileSize = tileSize,
                    TotalTilesAtZoom = (int)totalTilesAtZoom,
                    TileWorldSize = (long)tileWorldSize
                },
                TileUrl = new
                {
                    TemplateFormat = "https://atlas.microsoft.com/map/tile?subscription-key={subscription-key}&api-version=2022-08-01&tilesetId={tilesetId}&zoom={z}&x={x}&y={y}",
                    ExampleUrl = $"https://atlas.microsoft.com/map/tile?subscription-key={{subscription-key}}&api-version=2022-08-01&tilesetId=microsoft.base.road&zoom={zoomLevel}&x={tileIndex.X}&y={tileIndex.Y}"
                }
            };

            logger.LogInformation("Successfully calculated tile coordinates: X={X}, Y={Y}, Zoom={Zoom}", 
                tileIndex.X, tileIndex.Y, zoomLevel);

            return Task.FromResult(JsonSerializer.Serialize(new { success = true, result }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during tile coordinate calculation");
            return Task.FromResult(JsonSerializer.Serialize(new { error = "An unexpected error occurred" }));
        }
    }
}