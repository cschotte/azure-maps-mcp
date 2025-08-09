# 🗺️ Azure Maps ### Focused capabilities
- 🎯 Geocoding and reverse geocoding with optional admin boundaries
- 🛣️ Directions, route matrices, reachable range (isochrones)
- 🖼️ Static map rendering with markers and paths
- 🌐 IP geolocation and validation (IPv4/IPv6)
- 🌎 Country lookup and lightweight country search
- ⏰ Time zone information by coordinates with detailed options
- 🌤️ Weather data including current conditions, forecasts, and alertsrver

Make any MCP-compatible AI agent location-aware with Azure Maps—search, routes, boundaries, IP geolocation, time zones, weather, and static maps in one serverless package.

This project implements a Model Context Protocol (MCP) server on Azure Functions (isolated worker, .NET 9). It exposes a compact, pragmatic set of tools that map directly to Azure Maps capabilities, so agents can geocode, analyze coordinates, compute routes/matrices/ranges, render images, validate IPs, look up countries, get time zone information, and access weather data—securely and at scale.

> [!IMPORTANT]
> This MCP server is in preview. APIs, tool schemas, and behavior may change. Avoid production use.

## 🚀 Why choose this MCP server

### Built on Azure Maps
Tap into Microsoft’s global mapping platform with modern SDKs for search, routing, rendering, and geolocation.

### Serverless and scalable
Runs on Azure Functions with automatic scaling and pay-per-use economics. Zero infrastructure to manage.

### Focused capabilities
- 🎯 Geocoding and reverse geocoding with optional admin boundaries
- 🛣️ Directions, route matrices, reachable range (isochrones)
- �️ Static map rendering with markers and paths
- � IP geolocation and validation (IPv4/IPv6)
- 🌎 Country lookup and lightweight country search

### Developer-first
- ✅ Works with any MCP-compatible client
- ✅ Small, well-documented tool surface
- ✅ Strong typing and validation (.NET 9)
- ✅ Clear errors with consistent JSON envelopes

## 🎯 Great for

**🏢 Enterprise Applications**
- Customer location services and store locators
- Supply chain optimization and logistics planning
- Sales territory analysis and market research
- Emergency response and asset tracking

**🤖 AI-Powered Solutions**
- Conversational travel planning assistants
- Smart logistics and delivery optimization
- Location-aware business intelligence
- Geospatial data analysis and reporting

**🌐 Location-Based Services**
- Real estate and property management platforms
- Field service management systems
- Fleet management and route optimization
- Geographic market analysis tools

## 🔥 Capabilities

### 🗺️ Search & Geocoding
- Forward geocoding with address components and confidence
- Reverse geocoding with optional boundary geometry (locality, postal code, admin district, country)

### 🛣️ Routing & analysis
- Directions with traffic, leg summaries, and optional instruction text
- Matrix (all-to-all) travel times/distances for provided points
- Range analysis by time (minutes) or distance (km)
- Route analysis for border crossings and country context

### 🖼️ Static map rendering
- PNG output as a data URI for easy embedding
- Markers (labels) and polyline paths
- Road, satellite, or hybrid styles

### 🌐 IP geolocation
- Single and batch country-code lookup
- Format validation and private/loopback detection

### ⏰ Time zone services
- Time zone information by coordinates with customizable detail levels
- Historical timezone transitions and daylight saving time data
- Support for specific timestamps and transition year ranges

### 🌤️ Weather services
- Current weather conditions with temperature, humidity, wind, and precipitation
- Hourly forecasts up to 10 days with detailed meteorological data
- Daily forecasts up to 45 days with min/max temperatures and conditions
- Severe weather alerts with geographic area details

### ⚡ Performance & reliability
- Auto-scaling via Azure Functions (consumption or premium plans)
- Pay-per-use model; free-tier options available for Azure Maps
- Structured logging; Application Insights compatible

## ⚙️ MCP tools exposed

The tool catalog is registered as "Azure Maps Tool". These are the tool names and key parameters your MCP client will see:

### 🔍 Location
- location_find
  - query (string, required)
  - maxResults (number, 1–20, default 5)
  - includeBoundaries ("true"|"false", default "false")

- location_analyze
  - latitude (number), longitude (number)
  - boundaryType (locality|postalCode|adminDistrict|countryRegion, default locality)
  - resolution (small|medium|large, default small)

### 🛣️ Navigation
- navigation_calculate
  - coordinates (LatLon[]; for range: 1 point; for directions/matrix: ≥2)
  - calculationType (directions|matrix|range, default directions)
  - travelMode (car|truck|taxi|bus|van|motorcycle|bicycle|pedestrian, default car)
  - routeType (fastest|shortest, default fastest)
  - timeBudgetMinutes (number, for range)
  - distanceBudgetKm (number, for range)
  - avoidTolls ("true"|"false")
  - avoidHighways ("true"|"false")

- navigation_analyze
  - coordinates (LatLon[]; ≥2)

### 🖼️ Rendering
- render_staticmap
  - boundingBox (stringified JSON with west,south,east,north)
  - zoomLevel (1–20)
  - width,height (1–8192 px)
  - mapStyle (road|satellite|hybrid)
  - markers (array of { position:{latitude,longitude}, label?, color? })
  - paths (array of { coordinates: LatLon[], width?, color? })

### 🌐 Geolocation
- geolocation_ip: ipAddress (string)
- geolocation_ip_batch: ipAddresses (string[] up to 100)
- geolocation_ip_validate: ipAddress (string)

### 🌎 Countries
- search_country_info: countryCode (alpha-2 or alpha-3)
- search_countries: searchTerm (string), maxResults (1–50)

### ⏰ Time Zone
- timezone_by_coordinates
  - latitude (number), longitude (number)
  - options (none|zoneInfo|transitions|all, default none)
  - timeStamp (ISO 8601 string, optional)
  - transitionsFrom (ISO 8601 string, optional)
  - transitionsYears (number, 1-5, optional)

### 🌤️ Weather
- weather_current
  - latitude (number), longitude (number)
  - unit (metric|imperial, default metric)
  - duration (0|6|24 hours, default 0)
  - language (IETF language tag, optional)

- weather_hourly
  - latitude (number), longitude (number)
  - duration (1|12|24|72|120|240 hours, default 24)
  - unit (metric|imperial, default metric)
  - language (IETF language tag, optional)

- weather_daily
  - latitude (number), longitude (number)
  - duration (1|5|10|25|45 days, default 5)
  - unit (metric|imperial, default metric)
  - language (IETF language tag, optional)

- weather_alerts
  - latitude (number), longitude (number)
  - language (IETF language tag, optional)
  - details (true|false, default true)

## 🛠️ Get started

### Prerequisites
Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- Azure Maps subscription (free tier available)

### 🚀 Quick Setup

#### 1) Get your Azure Maps key

1. In the [Azure Portal](https://portal.azure.com), create an Azure Maps account (Gen2).
2. Under Authentication, use Shared Key Authentication and copy the Primary Key.

#### 2) Configure local settings

Create the `source/local.settings.json` file for local development:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "None",
    "AzureWebJobsSecretStorageType": "Files",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AZURE_MAPS_SUBSCRIPTION_KEY": "<your-key>"
  }
}
```

> 💡 **Pro Tip**: For production deployments, use [Azure Key Vault](https://docs.microsoft.com/azure/key-vault/) or Azure Functions application settings instead of hard-coding keys.

⚠️ **Security Best Practice**: The `local.settings.json` file is automatically ignored by Git. Never commit secrets to version control!

## 🔨 Build and run

### Lightning-Fast Development Setup

#### Terminal
```bash
# Clone and navigate to the project
git clone https://github.com/cschotte/azure-maps-mcp.git
cd azure-maps-mcp/source

# Restore dependencies and build (typically <30 seconds)
dotnet restore
dotnet build

# Start the local development server
func host start
```

#### Visual Studio Code
1. **Open the project**: `File` → `Open Folder` → Select `azure-maps-mcp`
2. **Install recommended extensions**: VS Code will prompt you automatically
3. **Build**: `Ctrl+Shift+P` → `Tasks: Run Task` → `build (functions)`
4. **Debug**: Press `F5` or click the "Run and Debug" button

The server starts in seconds and automatically watches for code changes with hot reload!

### 🚀 Run the MCP server

#### Development Mode (Hot Reload Enabled)
```bash
# Azure Functions Core Tools (recommended)
cd source && dotnet build && cd bin/Debug/net9.0
func host start
```

**Your server will be running at:**
- 🌐 HTTP: `http://localhost:7071` (Azure Functions default)
- 🔒 HTTPS: `https://localhost:7174` (VS Code launch profile)

#### VS Code integration
- **Quick Start**: Press `F5` for instant debugging with breakpoints
- **Task Runner**: `Ctrl+Shift+P` → `Tasks: Run Task` → `func: host start`
- **Integrated Terminal**: Built-in terminal with auto-completion and IntelliSense

### 🌍 Deploy to Azure

#### Deploy to Azure Functions (Serverless)
Optional commands you can adapt for your environment:

```bash
# Build for production
dotnet publish --configuration Release

# Create a Function App (consumption plan) and set settings
# Note: replace the placeholders to match your subscription/environment
az functionapp create \
  --resource-group <rg> \
  --consumption-plan-location <region> \
  --runtime dotnet-isolated \
  --functions-version 4 \
  --name <app-name>

az functionapp config appsettings set \
  --resource-group <rg> \
  --name <app-name> \
  --settings AZURE_MAPS_SUBSCRIPTION_KEY=<your-key>

# Deploy your code
func azure functionapp publish <app-name>
```

#### Alternative: Deploy using Azure DevOps or GitHub Actions
- **Continuous Deployment**: Automatic deployments on every commit
- **Environment Management**: Separate dev/staging/production environments  
- **Secret Management**: Secure handling of API keys and connection strings
- **Monitoring**: Built-in Application Insights and custom dashboards

Notes
- Configure monitoring with Application Insights for production environments.
- Store secrets in App Settings or Azure Key Vault; avoid committing secrets.

## 🏗️ Architecture & project structure

### Clean Architecture Design
Built following Microsoft's recommended patterns for maintainable, testable, and scalable serverless applications:

```
azure-maps-mcp/
├── 📁 source/                          # Main application source
│   ├── 📄 azure-maps-mcp.csproj       # Dependencies & build configuration
│   ├── 📄 Program.cs                   # DI container & application startup
│   ├── 📄 host.json                    # Azure Functions runtime configuration
│   ├── 📄 local.settings.json          # Development environment settings
│   │
│   ├── 📁 Services/                     # Business logic layer
│   │   ├── 📄 IAzureMapsService.cs     # Service abstraction (testable)
│   │   └── 📄 AzureMapsService.cs      # Azure Maps SDK implementation
│   │
│   └── 📁 Tools/                        # MCP tool implementations
│       ├── 📄 BaseMapsTool.cs          # Shared validation/response helpers
│       ├── 📄 LocationTool.cs          # 🔍 Geocoding, reverse geocoding, boundaries
│       ├── 📄 NavigationTool.cs        # 🛣️ Directions, matrix, range, analysis
│       ├── 📄 RenderTool.cs            # 🖼️ Static map generation
│       ├── 📄 GeolocationTool.cs       # 🌐 IP geolocation & validation
│       ├── 📄 CountryTool.cs           # 🌎 Country info & search
│       ├── 📄 TimeZoneTool.cs          # ⏰ Time zone information by coordinates
│       └── 📄 WeatherTool.cs           # 🌤️ Weather conditions, forecasts & alerts
│
├── 📄 README.md                         # This comprehensive guide
└── 📄 LICENSE                           # MIT license for commercial use
```

### Key Design Principles

**Separation of concerns**
- **Tools Layer**: MCP protocol handling and input validation
- **Services Layer**: Business logic and Azure Maps SDK integration  
- **Clean Interfaces**: Dependency injection for testability and flexibility

**Performance oriented**
- **Async/Await**: Non-blocking I/O throughout the application
- **Memory Efficient**: Minimal allocations with streaming where possible
- **Cached Connections**: Reusable HTTP clients for optimal throughput

**Security by design**
- **Input Validation**: Comprehensive parameter sanitization
- **Secret Management**: Environment-based configuration
- **Error Handling**: Secure error messages without information leakage

## 📦 Dependencies & stack

### Azure Maps SDKs (as used here)
- Azure.Maps.Search `2.0.0-beta.5`
- Azure.Maps.Routing `1.0.0-beta.4`
- Azure.Maps.Rendering `2.0.0-beta.1`
- Azure.Maps.Geolocation `1.0.0-beta.3`

### Azure Functions stack
- Microsoft.Azure.Functions.Worker `2.0.0` (isolated)
- Microsoft.Azure.Functions.Worker.Extensions.Mcp `1.0.0-preview.6`

### Additional libraries
- CountryData.Standard `1.5.0`
- .NET 9.0

### Why this stack
- Strong typing and modern SDKs
- Cross-platform local dev on macOS, Linux, and Windows
- Simple to deploy and scale with Azure Functions

## ⚙️ Configuration

### Environment Variables
Required
- `AZURE_MAPS_SUBSCRIPTION_KEY` – your Azure Maps key

Optional
- `FUNCTIONS_WORKER_RUNTIME` – defaults to `dotnet-isolated`
- `AzureWebJobsStorage` – set to `"None"` for local dev

### Azure Functions host settings (host.json)
```json
{
  "version": "2.0",
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true
      }
    }
  }
}
```

### Advanced Configuration Options
- **Custom Timeouts**: Configure per-tool timeout limits
- **Rate Limiting**: Implement custom throttling for cost control
- **Caching**: Add Redis cache for frequently requested data
- **Monitoring**: Application Insights integration for performance tracking

## 🔧 Troubleshooting

### Common Setup Issues

"Subscription key is invalid"
- ✅ Verify your key in the Azure Portal under Azure Maps → Authentication
- ✅ Check that the key is correctly set in `local.settings.json`
- ✅ Ensure no extra spaces or quotation marks around the key

"Build failed – SDK not found"
- ✅ Install .NET 9.0 SDK from [Microsoft's official site](https://dotnet.microsoft.com/download/dotnet/9.0)
- ✅ Run `dotnet --version` to verify installation
- ✅ Clear NuGet cache: `dotnet nuget locals all --clear`

"Function host won't start"
- ✅ Update Azure Functions Core Tools: `npm install -g azure-functions-core-tools@4`
- ✅ Check port availability (7071, 7174)
- ✅ Verify `local.settings.json` format with a JSON validator

### Performance Optimization Tips

Performance tips
- Use batch operations (e.g., `geolocation_ip_batch`) for multiple requests
- Implement client-side caching for frequently accessed country data
- Consider Azure Front Door for global distribution

Cost tips
- Monitor usage in Azure Portal to stay within free tier limits
- Cache static map images to avoid regeneration
- Use route matrix efficiently for bulk calculations

### Production checklist

- [ ] **Security**: API keys stored in Azure Key Vault or app settings
- [ ] **Monitoring**: Application Insights configured with custom metrics
- [ ] **Scaling**: Consumption plan configured for automatic scaling
- [ ] **Networking**: Virtual network integration for security (if required)
- [ ] **Backup**: Source code in version control with automated deployments
- [ ] **Testing**: Load testing completed for expected traffic volumes

## 📚 Resources

- [Azure Maps Documentation](https://docs.microsoft.com/azure/azure-maps/) - Comprehensive API guides
- [Model Context Protocol Specification](https://spec.modelcontextprotocol.io/) - MCP protocol details
- [Azure Functions Best Practices](https://docs.microsoft.com/azure/azure-functions/functions-best-practices) - Performance optimization

## 🆘 Support
- Use GitHub Issues for bugs and feature requests
- For Azure Maps questions, see the docs and community resources linked above

## Contributing

This project welcomes contributions and suggestions. Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com).

## Trademarks
This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party's policies.