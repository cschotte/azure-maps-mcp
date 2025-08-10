# Azure Maps MCP Server

A Model Context Protocol (MCP) server implementation that provides **Azure Maps** functionality as tools for Large Language Models (LLMs). This server exposes the full range of Azure Maps services including search, routing, rendering, and geolocation capabilities.

> [!IMPORTANT]
> The **Azure Maps MCP Server** is currently in preview. You can expect changes prior to the MCP server becoming generally available.
> You should avoid using this MCP server preview in production apps.

## Overview

This project implements an MCP server using Azure Functions that integrates with Azure Maps services and comprehensive country data. It allows LLMs to perform a wide range of geographic operations including:

- **Geocoding & Search**: Convert addresses or place names to coordinates and vice versa
- **Routing**: Calculate routes, travel times and reachable areas
 - **Map Rendering**: Generate static map images
- **Geolocation**: Determine country codes and location info from IP addresses
- **Country Intelligence**: Access basic country information and search
 - **Time Zones & Weather**: Query time zone info and weather conditions/forecasts

## ⚙️ Supported Tools

Interact with these Azure Maps services through the following MCP tools:

### 🔍 Location search & analysis

- location_find: Find locations by address/place name. Returns coordinates, formatted address, components, confidence, and optional boundaries.
- location_analyze: Reverse geocode coordinates and return address plus administrative boundary polygons (locality, postalCode, adminDistrict, countryRegion).
- search_country_info: Get country info by ISO 3166-1 code (alpha-2 or alpha-3), e.g., US/USA.
- search_countries: Search countries by name or code with optional max result limit.

### 🛣️ Routing & navigation

- navigation_calculate: Universal route calculation. Use calculationType = directions | matrix | range. Options include travelMode, routeType, avoidTolls, avoidHighways, and time/distance budgets for range.
- navigation_analyze: Analyze a series of waypoints for international travel, border crossings, and related considerations.

### 🖼️ Map rendering

- render_staticmap: Generate static PNG map images for a bounding box with optional markers and paths. Returns a data URI.

### 🌐 Geolocation & IP analysis

- geolocation_ip: Get country code and name for a public IPv4/IPv6 address.
- geolocation_ip_batch: Batch version for up to 100 IP addresses in one call.
- geolocation_ip_validate: Validate IP address format and identify traits (family, loopback, private/public).

### 🕰️ Time zones

- timezone_by_coordinates: Time zone details (offsets, DST, names, sunrise/sunset) for latitude/longitude.

### ⛅ Weather

- weather_current: Current conditions at coordinates (temperature, precip, wind, humidity, etc.).
- weather_hourly: Hourly forecast for 1/12/24/72/120/240 hours.
- weather_daily: Daily forecast for 1/5/10/25/45 days.
- weather_alerts: Severe weather alerts near coordinates.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
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
- build (functions): Clean + build the Functions project
- func: host start: Start the local Functions host (depends on build)
- publish (functions): Publish the project in Release configuration

Tip: Use Command+Shift+P on macOS and search for "Tasks: Run Task".

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

The local HTTP endpoint is typically http://localhost:7071 when using Azure Functions Core Tools. The launch setting port 7174 refers to the internal worker port and is not the HTTP port.

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
│   ├── Program.cs                 # Functions isolated worker + MCP configuration
│   ├── host.json                  # Azure Functions host configuration
│   ├── local.settings.json        # Local development settings (do not commit real secrets)
│   ├── Services/
│   │   ├── IAzureMapsService.cs   # Service interface
│   │   └── AzureMapsService.cs    # Azure Maps SDK clients
│   └── Tools/
│       ├── LocationTool.cs        # location_find, location_analyze
│       ├── NavigationTool.cs      # navigation_calculate, navigation_analyze
│       ├── RenderTool.cs          # render_staticmap
│       ├── GeolocationTool.cs     # geolocation_ip, geolocation_ip_batch, geolocation_ip_validate
│       ├── CountryTool.cs         # search_country_info, search_countries
│       ├── TimeZoneTool.cs        # timezone_by_coordinates
│       └── WeatherTool.cs         # weather_current, weather_hourly, weather_daily, weather_alerts
└── README.md
```

## Dependencies

- Azure.Maps.Search (2.0.0-beta.5)
- Azure.Maps.Routing (1.0.0-beta.4)
- Azure.Maps.Rendering (2.0.0-beta.1)
- Azure.Maps.Geolocation (1.0.0-beta.3)
- CountryData.Standard (1.5.0)
- Microsoft.Azure.Functions.Worker (2.0.0)
- Microsoft.Azure.Functions.Worker.Extensions.Mcp (1.0.0-preview.6)
- .NET 9.0 target framework

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
- For general questions about Azure Maps, visit [Azure Maps Documentation](https://learn.microsoft.com/azure/azure-maps/)
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