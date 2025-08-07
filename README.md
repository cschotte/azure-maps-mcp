# Azure Maps MCP Server

A Model Context Protocol (MCP) server implementation that provides **Azure Maps** functionality as tools for Large Language Models (LLMs). This server exposes the full range of Azure Maps services including search, routing, rendering, and geolocation capabilities.

## Overview

This project implements an MCP server using Azure Functions that integrates with Azure Maps services and comprehensive country data. It allows LLMs to perform a wide range of geographic operations including:

- **Geocoding & Search**: Convert addresses or place names to coordinates and vice versa
- **Routing**: Calculate routes, travel times and reachable areas
- **Map Rendering**: Generate map tiles and static map images
- **Geolocation**: Determine country codes and location info from IP addresses
- **Country Intelligence**: Access comprehensive country information

## Features

### 🗺️ Search & Geocoding
Convert street addresses, landmarks, or place names to geographic coordinates with detailed address information:
- **Geocoding**: Address → Coordinates with detailed properties
- **Reverse Geocoding**: Coordinates → Human-readable addresses
- **Administrative Boundaries**: Retrieve polygon boundaries for cities, postal codes, states, countries
- **Country Information**: Access comprehensive country data by ISO codes
- **Country Search**: Find countries by name, continent, or geographic criteria
- Confidence scores, match codes, and comprehensive address details

### 🛣️ Routing & Navigation
Calculate optimal routes and analyze travel patterns:
- **Route Directions**: Turn-by-turn navigation between multiple points
- **Route Matrix**: Calculate travel times/distances between multiple origins and destinations
- **Route Range (Isochrone)**: Find areas reachable within time or distance budgets
- Support for multiple travel modes (car, truck, bicycle, pedestrian, etc.)
- Traffic-aware routing and route optimization options

### 🖼️ Map Rendering & Visualization
Generate visual map content and tiles:
- **Map Tiles**: Retrieve individual map tiles for custom mapping applications
- **Static Map Images**: Generate map snapshots with custom markers and paths
- Support for road, satellite, and hybrid map styles

### 🌐 Geolocation & IP Analysis
Determine geographic information from IP addresses:
- **IP Geolocation**: Get country codes from IPv4/IPv6 addresses
- **Batch IP Processing**: Process multiple IP addresses efficiently
- **IP Validation**: Validate IP address formats and get technical details
- Support for both public and private IP address analysis

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Azure Functions Core Tools](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- Azure Maps subscription key

## Setup

### 1. Get Azure Maps Subscription Key

1. Create an Azure Maps account in the [Azure Portal](https://portal.azure.com)
2. Create a new Azure Maps resource
3. Copy the subscription key from the resource's authentication settings

### 2. Configure Environment

Create or update the `source/local.settings.json` file with your Azure Maps subscription key:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "None",
    "AzureWebJobsSecretStorageType": "Files",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AZURE_MAPS_SUBSCRIPTION_KEY": "your-azure-maps-subscription-key-here"
  }
}
```

⚠️ **Important**: Never commit your actual subscription key to version control. Use environment variables or Azure Key Vault for production deployments.

## Building the Project

### Clean and Build
```bash
# Navigate to the project directory
cd source

# Clean the project
dotnet clean

# Restore dependencies and build
dotnet build
```

### Using VS Code Tasks
If you're using VS Code, you can use the predefined tasks:
- `Ctrl+Shift+P` → "Tasks: Run Task" → "build (functions)"

## Running the Server

### Local Development

#### Option 1: Using Azure Functions Core Tools
```bash
# Navigate to the source directory
cd source

# Build the project first
dotnet build

# Start the function host
cd bin/Debug/net9.0
func host start
```

#### Option 2: Using VS Code
- Press `F5` or use "Run and Debug" in VS Code
- Or run the task: `Ctrl+Shift+P` → "Tasks: Run Task" → "func: host start"

#### Option 3: Using .NET CLI
```bash
# Navigate to the source directory
cd source

# Run the application
dotnet run
```

The server will start on the default port (typically 7071 for Azure Functions, or 7174 as configured in launch settings).

### Production Deployment

#### Azure Functions
Deploy to Azure Functions for production use:

```bash
# Publish the project
dotnet publish --configuration Release

# Deploy using Azure Functions Core Tools
func azure functionapp publish your-function-app-name
```

## Usage

Once running, the MCP server exposes the following tools organized by service category:

### 🗺️ Search Tools

#### Geocoding
```json
{
  "name": "geocoding",
  "description": "Convert street addresses or place names to longitude and latitude coordinates",
  "parameters": {
    "address": "string - Address or landmark name",
    "maxResults": "number - Maximum results to return (1-100, default: 5)"
  }
}
```

#### Reverse Geocoding
```json
{
  "name": "reverse_geocoding", 
  "description": "Convert longitude and latitude coordinates to a street address",
  "parameters": {
    "latitude": "number - Latitude coordinate",
    "longitude": "number - Longitude coordinate"
  }
}
```

#### Get Polygon
```json
{
  "name": "get_polygon",
  "description": "Get administrative boundary polygon for a specific location",
  "parameters": {
    "latitude": "number - Latitude coordinate",
    "longitude": "number - Longitude coordinate", 
    "resultType": "string - locality|postalCode1|adminDistrict1|adminDistrict2|countryRegion",
    "resolution": "string - small|medium|large"
  }
}
```

#### Get Country Information
```json
{
  "name": "get_country_info",
  "description": "Get comprehensive country information including demographics, geography, economics, and cultural data by ISO country code",
  "parameters": {
    "countryCode": "string - ISO 3166-1 alpha-2 country code (e.g., 'US', 'DE', 'JP')"
  }
}
```

#### Find Countries
```json
{
  "name": "find_countries",
  "description": "Find countries by name, continent, or other criteria for regional analysis and geographic data exploration",
  "parameters": {
    "searchTerm": "string - Search term (partial country name, continent name like 'Europe', or other geographic identifier)",
    "maxResults": "number - Maximum countries to return (1-50, default: 10)"
  }
}
```

### 🛣️ Routing Tools

#### Get Route Directions
```json
{
  "name": "get_route_directions",
  "description": "Calculate route directions between coordinates with turn-by-turn instructions",
  "parameters": {
    "coordinates": "array - Array of lat/lng objects for waypoints",
    "travelMode": "string - car|truck|bicycle|pedestrian (default: car)",
    "routeType": "string - fastest|shortest|eco (default: fastest)",
    "avoidTolls": "boolean - Avoid toll roads",
    "avoidHighways": "boolean - Avoid highways"
  }
}
```

#### Get Route Matrix
```json
{
  "name": "get_route_matrix",
  "description": "Calculate travel times and distances between multiple origins and destinations",
  "parameters": {
    "origins": "array - Array of origin coordinate objects",
    "destinations": "array - Array of destination coordinate objects",
    "travelMode": "string - car|truck|bicycle|pedestrian (default: car)",
    "routeType": "string - fastest|shortest|eco (default: fastest)"
  }
}
```

#### Get Route Range
```json
{
  "name": "get_route_range",
  "description": "Calculate reachable area within time or distance budget (isochrone)",
  "parameters": {
    "latitude": "number - Starting latitude",
    "longitude": "number - Starting longitude",
    "timeBudgetInSeconds": "number - Time budget (OR distanceBudgetInMeters)",
    "distanceBudgetInMeters": "number - Distance budget (OR timeBudgetInSeconds)",
    "travelMode": "string - car|truck|bicycle|pedestrian (default: car)"
  }
}
```

#### Analyze Route Countries
```json
{
  "name": "analyze_route_countries",
  "description": "Analyze a route and identify all countries that the route passes through for international travel planning and customs preparation",
  "parameters": {
    "coordinates": "string - JSON array of coordinate objects representing the route path (minimum 2 points required)"
  }
}
```

### 🖼️ Rendering Tools

#### Get Map Tile
```json
{
  "name": "get_map_tile",
  "description": "Retrieve a map tile image for specific geographic coordinates and zoom level with various styles",
  "parameters": {
    "latitude": "number - Latitude coordinate for tile center",
    "longitude": "number - Longitude coordinate for tile center",
    "zoomLevel": "number - Zoom level (1-22, default: 10)",
    "tileSetId": "string - Map style: microsoft.base.road|microsoft.base.hybrid|microsoft.imagery",
    "tileSize": "number - 256 or 512 pixels (default: 256)"
  }
}
```

#### Get Static Map Image
```json
{
  "name": "get_static_map_image",
  "description": "Generate a custom static map image with optional markers and path overlays for reports and presentations",
  "parameters": {
    "boundingBox": "string - JSON object with {west, south, east, north} coordinates",
    "zoomLevel": "number - Zoom level (1-20, default: 10)",
    "width": "number - Image width in pixels (1-8192, default: 512)",
    "height": "number - Image height in pixels (1-8192, default: 512)",
    "mapStyle": "string - road|satellite|hybrid (default: road)",
    "markers": "string - Optional JSON array of markers with lat/lng/label/color",
    "paths": "string - Optional JSON array of paths with coordinates and styling"
  }
}
```

### 🌐 Geolocation Tools

#### Get Country Code by IP
```json
{
  "name": "get_country_code_by_ip",
  "description": "Get country code and location info for an IP address",
  "parameters": {
    "ipAddress": "string - IPv4 or IPv6 address to look up"
  }
}
```

#### Get Country Code Batch
```json
{
  "name": "get_country_code_batch",
  "description": "Get country codes for multiple IP addresses",
  "parameters": {
    "ipAddresses": "array - Array of IP addresses (max 100)"
  }
}
```

#### Validate IP Address
```json
{
  "name": "validate_ip_address",
  "description": "Validate IP address format and get technical details",
  "parameters": {
    "ipAddress": "string - IP address to validate"
  }
}
```

## Project Structure

```
azure-maps-mcp/
├── source/
│   ├── azure-maps-mcp.csproj     # Project file with dependencies
│   ├── Program.cs                 # Application entry point and DI configuration
│   ├── host.json                  # Azure Functions host configuration
│   ├── local.settings.json        # Local development settings (not in repo)
│   ├── Services/
│   │   ├── IAzureMapsService.cs   # Service interface
│   │   └── AzureMapsService.cs    # Azure Maps client implementation
│   └── Tools/
│       ├── SearchTool.cs          # MCP tools for geocoding, search, and country data
│       ├── RoutingTool.cs         # MCP tools for routing and route analysis
│       ├── RenderTool.cs          # MCP tools for map tiles and static images
│       └── GeolocationTool.cs     # MCP tools for IP geolocation and validation
└── README.md
```

## Dependencies

- **Azure.Maps.Search** (2.0.0-beta.5): Azure Maps Search SDK
- **Azure.Maps.Routing** (1.0.0-beta.4): Azure Maps Routing SDK
- **Azure.Maps.Rendering** (1.0.0-beta.4): Azure Maps Rendering SDK
- **Azure.Maps.Geolocation** (1.0.0-beta.3): Azure Maps Geolocation SDK
- **CountryData.Standard** (1.5.0): Comprehensive country information library
- **Microsoft.Azure.Functions.Worker** (2.0.0): Azure Functions runtime
- **Microsoft.Azure.Functions.Worker.Extensions.Mcp** (1.0.0-preview.6): MCP support for Azure Functions
- **.NET 9.0**: Target framework

## Configuration

### Environment Variables
- `AZURE_MAPS_SUBSCRIPTION_KEY`: Your Azure Maps subscription key (required)

### Azure Functions Settings
- `AzureWebJobsStorage`: Set to "None" for local development
- `FUNCTIONS_WORKER_RUNTIME`: Set to "dotnet-isolated"

## Troubleshooting

### Common Issues

1. **Missing subscription key**: Ensure `AZURE_MAPS_SUBSCRIPTION_KEY` is set in your environment
2. **Build failures**: Make sure you have .NET 9.0 SDK installed
3. **Function startup issues**: Check that Azure Functions Core Tools are installed and up to date

### Logs
The application uses structured logging. Check the console output for detailed error messages and operational information.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the terms specified in the LICENSE file.

## Related Links

- [Azure Maps Documentation](https://docs.microsoft.com/en-us/azure/azure-maps/)
- [Model Context Protocol Specification](https://modelcontextprotocol.io/)
- [Azure Functions Documentation](https://docs.microsoft.com/en-us/azure/azure-functions/)
