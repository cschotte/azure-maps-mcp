# Azure Maps MCP Server

A Model Context Protocol (MCP) server implementation that provides **Azure Maps** functionality as tools for Large Language Models (LLMs). This server exposes the full range of Azure Maps services including search, routing, rendering, and geolocation capabilities.

> [!IMPORTANT]
> The **Azure Maps MCP Server** is currently in preview. You can expect changes prior to the MCP server becoming generally available.
> You should avoid using this MCP server preview in production apps.

## Overview

This project implements an MCP server using Azure Functions that integrates with Azure Maps services and comprehensive country data. It allows LLMs to perform a wide range of geographic operations including:

- **Geocoding & Search**: Convert addresses or place names to coordinates and vice versa
- **Routing**: Calculate routes, travel times and reachable areas
- **Map Rendering**: Generate map tiles and static map images
- **Geolocation**: Determine country codes and location info from IP addresses
- **Country Intelligence**: Access comprehensive country information

## ⚙️ Supported Tools

Interact with these Azure Maps services through the following MCP tools:

### 🔍 Search & Geocoding

**search_geocoding**: Convert street addresses, landmarks, or place names into precise geographic coordinates (latitude and longitude). Handles various address formats and returns detailed address components with confidence scores.

**search_geocoding_reverse**: Convert geographic coordinates into human-readable street addresses and location details. Essential for location-based applications displaying meaningful address information from GPS coordinates.

**search_polygon**: Retrieve administrative boundary polygons for geographic locations such as city limits, postal code areas, state/province boundaries, or country borders. Returns precise polygon coordinates for spatial analysis.

**search_country_info**: Get comprehensive country information including demographics, geography, economics, and cultural data by ISO country code. Returns languages, currencies, time zones, calling codes, and more.

**search_countries**: Find countries by name, continent, or other criteria. Helps discover countries matching specific geographic, cultural, or economic characteristics.

### 🛣️ Routing & Navigation

**routing_directions**: Calculate detailed driving/walking/cycling directions between geographic coordinates. Returns comprehensive route information including distance, travel time, turn-by-turn navigation, and route geometry.

**routing_matrix**: Calculate travel times and distances between multiple origin and destination points in matrix format. Essential for delivery route planning, finding closest locations, and logistics optimization.

**routing_range**: Calculate geographic areas reachable within specified time or distance limits from a starting point. Creates isochrone/isodistance polygons for service area analysis and accessibility studies.

**routing_countries**: Analyze routes and identify all countries the route passes through. Valuable for international travel planning, customs preparation, and cross-border logistics.

### 🖼️ Map Rendering

**render_staticmap**: Generate custom static map images for specified geographic areas with optional markers and path overlays. Creates publication-ready map images perfect for reports, documentation, and presentations.

### 🌐 Geolocation & IP Analysis

**geolocation_ip**: Get country code and location information (ISO code, country name, continent) for a given IP address. Supports both IPv4 and IPv6 addresses.

**geolocation_ip_batch**: Get country codes and location information for multiple IP addresses in a single request. Efficiently processes up to 100 IP addresses at once.

**geolocation_ip_validate**: Validate IP address format and get comprehensive technical information. Returns validation status, address family, scope information, and technical details.

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
    "AZURE_MAPS_SUBSCRIPTION_KEY": "<your-azure-maps-subscription-key-here>"
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

## Microsoft Open Source

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Support

This project is supported by Microsoft. For support and questions:

- Create an issue in this repository for bugs and feature requests
- For general questions about Azure Maps, visit [Azure Maps Documentation](https://docs.microsoft.com/azure/azure-maps/)
- For commercial support, contact [Azure Support](https://azure.microsoft.com/support/)

## Contributing

This project welcomes contributions and suggestions. Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.