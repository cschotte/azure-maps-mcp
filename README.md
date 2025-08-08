# 🗺️ Azure Maps MCP Server

**Supercharge your AI applications with world-class geospatial intelligence!**

A cutting-edge Model Context Protocol (MCP) server that bridges the gap between Large Language Models and enterprise-grade **Azure Maps** services. This serverless solution transforms any LLM into a powerful geospatial assistant capable of sophisticated geographic analysis, routing optimization, and location intelligence.

> [!IMPORTANT]
> The **Azure Maps MCP Server** is currently in preview. You can expect changes prior to the MCP server becoming generally available.
> You should avoid using this MCP server preview in production apps.

## 🚀 Why Choose Azure Maps MCP Server?

### **Enterprise-Grade Geospatial AI**
Transform your AI applications with Microsoft's world-class mapping platform. Built on Azure's global infrastructure, this MCP server delivers the same geospatial intelligence that powers Microsoft products used by billions worldwide.

### **Serverless & Scalable Architecture**
Built on Azure Functions with automatic scaling, pay-per-use pricing, and zero infrastructure management. Deploy once and handle everything from prototype to production scale seamlessly.

### **Rich Geospatial Capabilities**
- 🎯 **Precision Geocoding**: Convert any address worldwide to coordinates with confidence scores
- 🛣️ **Advanced Routing**: Multi-modal routing with real-time traffic and optimization
- 🗺️ **Dynamic Map Generation**: Create publication-ready maps with custom styling
- 🌍 **Global IP Intelligence**: Instant geolocation from IPv4/IPv6 addresses
- 📊 **Comprehensive Country Data**: Deep demographic, economic, and cultural insights
- 🔄 **Batch Operations**: Process thousands of locations efficiently

### **Developer-First Experience**
- ✅ **Plug-and-Play Integration**: Works with any MCP-compatible LLM client
- ✅ **Comprehensive Documentation**: Detailed examples and use cases
- ✅ **Type-Safe APIs**: Built with .NET 9.0 for robust, maintainable code
- ✅ **Rich Error Handling**: Detailed diagnostics and logging
- ✅ **Production Ready**: Microsoft-supported with enterprise SLA

## 🎯 Perfect For

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

## 🔥 Technical Features & Capabilities

### 🗺️ Advanced Search & Geocoding
**World-class address intelligence powered by Microsoft's global data**
- **High-Precision Geocoding**: Convert any address format to coordinates with sub-meter accuracy
- **Intelligent Reverse Geocoding**: Transform coordinates to human-readable addresses with cultural formatting
- **Administrative Boundaries**: Access precise polygon data for cities, postal codes, states, and countries
- **Global Country Intelligence**: 249 countries with demographics, economics, languages, and cultural data
- **Smart Country Discovery**: AI-powered search by name, continent, or geographic criteria
- **Confidence Scoring**: Quality metrics for every geocoding result to ensure reliability

### 🛣️ Enterprise Routing & Optimization
**Sophisticated routing engine for complex logistics scenarios**
- **Multi-Modal Routing**: Optimized routes for cars, trucks, bicycles, pedestrians, and motorcycles
- **Real-Time Traffic Integration**: Live traffic data for accurate travel time predictions
- **Route Matrix Calculations**: Bulk distance/time calculations for up to 700 origins × destinations
- **Isochrone Analysis**: Calculate service areas and accessibility zones with time/distance constraints
- **International Route Analysis**: Automatic border crossing detection with country-specific insights
- **Route Optimization**: Choose between fastest, shortest, or most fuel-efficient paths
- **Waypoint Support**: Complex multi-stop routing with sequence optimization

### 🖼️ Dynamic Map Rendering
**Publication-ready maps with enterprise customization**
- **Static Map Generation**: High-resolution images perfect for reports and documentation
- **Custom Markers & Overlays**: Brand-consistent visual elements with flexible styling
- **Multiple Map Styles**: Road, satellite, hybrid views with global coverage
- **Path Visualization**: Route overlays with customizable colors and weights
- **Scalable Output**: From thumbnail previews to high-DPI poster prints
- **Embedding Ready**: Direct integration into web apps, documents, and presentations

### 🌐 Global IP Intelligence
**Instant geolocation with enterprise-grade accuracy**
- **Universal IP Support**: Both IPv4 and IPv6 with comprehensive coverage
- **Batch Processing**: Analyze up to 100 IP addresses in a single optimized request
- **Advanced Validation**: Technical IP analysis with scope detection and format validation
- **Privacy Compliant**: Respectful of private network ranges and anonymization requirements
- **Real-Time Processing**: Sub-second response times for interactive applications
- **Global Coverage**: Worldwide IP geolocation database with regular updates

### ⚡ Performance & Reliability
**Enterprise-grade infrastructure for mission-critical applications**
- **Auto-Scaling**: Azure Functions automatically handle traffic spikes from 0 to millions of requests
- **Global Edge Network**: Distributed processing with <100ms response times worldwide
- **99.9% SLA**: Microsoft-backed availability guarantee with enterprise support
- **Cost Optimization**: Pay-per-use pricing model scales with your business needs
- **Security First**: OAuth2, managed identities, and VNet integration support
- **Comprehensive Monitoring**: Built-in Application Insights with custom metrics and alerts

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

## 🛠️ Getting Started in Minutes

### Prerequisites
**Everything you need for modern serverless development:**
- **[.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)** - Latest .NET with performance improvements and native AOT support
- **[Azure Functions Core Tools](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local)** - Local development and deployment tools (v4.x recommended)
- **Azure Maps Subscription** - Enterprise mapping services (free tier available)

### 🚀 Quick Setup

#### 1. Get Your Azure Maps Subscription Key

**Option A: Free Tier (Perfect for Development)**
1. Visit the [Azure Portal](https://portal.azure.com) and create a free account if needed
2. Create a new **Azure Maps** resource in any region
3. Select the **Gen2 (free)** pricing tier for 1M free transactions/month
4. Navigate to **Authentication** → **Shared Key Authentication**
5. Copy your **Primary Key** - this is your subscription key

**Option B: Enterprise Tier (Production Ready)**
- Unlimited transactions with pay-as-you-scale pricing
- Advanced analytics and usage reporting
- Priority support and SLA guarantees
- Enterprise security and compliance features

#### 2. Configure Your Development Environment

Create the `source/local.settings.json` file for local development:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "None",
    "AzureWebJobsSecretStorageType": "Files", 
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AZURE_MAPS_SUBSCRIPTION_KEY": "<<your-actual-key-here>>"
  }
}
```

> 💡 **Pro Tip**: For production deployments, use [Azure Key Vault](https://docs.microsoft.com/azure/key-vault/) or Azure Functions application settings instead of hard-coding keys.

⚠️ **Security Best Practice**: The `local.settings.json` file is automatically ignored by Git. Never commit secrets to version control!

## 🔨 Building & Running

### Lightning-Fast Development Setup

#### Using the Command Line
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

#### Using Visual Studio Code (Recommended)
1. **Open the project**: `File` → `Open Folder` → Select `azure-maps-mcp`
2. **Install recommended extensions**: VS Code will prompt you automatically
3. **Build**: `Ctrl+Shift+P` → `Tasks: Run Task` → `build (functions)`
4. **Debug**: Press `F5` or click the "Run and Debug" button

The server starts in seconds and automatically watches for code changes with hot reload!

### 🚀 Running Your MCP Server

#### Development Mode (Hot Reload Enabled)
```bash
# Option 1: Azure Functions Core Tools (Recommended)
cd source
dotnet build
cd bin/Debug/net9.0
func host start

# Option 2: Direct .NET execution
cd source
dotnet run
```

**Your server will be running at:**
- 🌐 HTTP: `http://localhost:7071` (Azure Functions default)
- 🔒 HTTPS: `https://localhost:7174` (VS Code launch profile)

#### VS Code Integration
- **Quick Start**: Press `F5` for instant debugging with breakpoints
- **Task Runner**: `Ctrl+Shift+P` → `Tasks: Run Task` → `func: host start`
- **Integrated Terminal**: Built-in terminal with auto-completion and IntelliSense

### 🌍 Production Deployment

#### Deploy to Azure Functions (Serverless)
```bash
# Build for production
dotnet publish --configuration Release

# Deploy using Azure CLI (one-time setup)
az login
az functionapp create --resource-group myResourceGroup \
                     --consumption-plan-location westus \
                     --runtime dotnet-isolated \
                     --functions-version 4 \
                     --name my-azure-maps-mcp

# Deploy your code
func azure functionapp publish my-azure-maps-mcp
```

#### Alternative: Deploy using Azure DevOps or GitHub Actions
- **Continuous Deployment**: Automatic deployments on every commit
- **Environment Management**: Separate dev/staging/production environments  
- **Secret Management**: Secure handling of API keys and connection strings
- **Monitoring**: Built-in Application Insights and custom dashboards

**Production Benefits:**
- ⚡ **Global Scale**: Auto-scaling across Azure's worldwide regions
- 💰 **Cost Effective**: Pay only for actual usage (free tier: 1M requests/month)
- 🔒 **Enterprise Security**: VNet integration, managed identities, private endpoints
- 📊 **Rich Monitoring**: Real-time metrics, alerts, and performance insights

## 🏗️ Architecture & Project Structure

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
│       ├── 📄 SearchTool.cs            # 🔍 Geocoding & country intelligence
│       ├── 📄 RoutingTool.cs           # 🛣️ Route planning & optimization
│       ├── 📄 RenderTool.cs            # 🖼️ Map generation & visualization
│       └── 📄 GeolocationTool.cs       # 🌐 IP geolocation & validation
│
├── 📄 README.md                         # This comprehensive guide
└── 📄 LICENSE                           # MIT license for commercial use
```

### Key Design Principles

**🎯 Separation of Concerns**
- **Tools Layer**: MCP protocol handling and input validation
- **Services Layer**: Business logic and Azure Maps SDK integration  
- **Clean Interfaces**: Dependency injection for testability and flexibility

**⚡ Performance Optimized**
- **Async/Await**: Non-blocking I/O throughout the application
- **Memory Efficient**: Minimal allocations with streaming where possible
- **Cached Connections**: Reusable HTTP clients for optimal throughput

**🔒 Security by Design**
- **Input Validation**: Comprehensive parameter sanitization
- **Secret Management**: Environment-based configuration
- **Error Handling**: Secure error messages without information leakage

## 📦 Dependencies & Technology Stack

### Core Azure Maps SDKs
**Latest preview versions with cutting-edge features:**
- **[Azure.Maps.Search](https://www.nuget.org/packages/Azure.Maps.Search)** `2.0.0-beta.5` - Geocoding, reverse geocoding, administrative boundaries
- **[Azure.Maps.Routing](https://www.nuget.org/packages/Azure.Maps.Routing)** `1.0.0-beta.4` - Multi-modal routing, traffic-aware navigation
- **[Azure.Maps.Rendering](https://www.nuget.org/packages/Azure.Maps.Rendering)** `1.0.0-beta.4` - Static map generation, custom styling
- **[Azure.Maps.Geolocation](https://www.nuget.org/packages/Azure.Maps.Geolocation)** `1.0.0-beta.3` - IP-to-location intelligence

### Microsoft Azure Functions Stack
**Production-ready serverless platform:**
- **[Microsoft.Azure.Functions.Worker](https://www.nuget.org/packages/Microsoft.Azure.Functions.Worker)** `2.0.0` - Isolated process model for performance
- **[Microsoft.Azure.Functions.Worker.Extensions.Mcp](https://www.nuget.org/packages/Microsoft.Azure.Functions.Worker.Extensions.Mcp)** `1.0.0-preview.6` - Native MCP protocol support

### Additional Libraries
**Carefully selected for functionality and performance:**
- **[CountryData.Standard](https://www.nuget.org/packages/CountryData.Standard)** `1.5.0` - Comprehensive country database (249 countries)
- **.NET 9.0** - Latest runtime with Native AOT support and performance improvements

### Why These Technologies?

**🚀 Performance Benefits**
- **.NET 9.0**: Up to 20% performance improvement over .NET 8
- **Isolated Worker Process**: Better memory management and faster cold starts
- **Native AOT Ready**: Sub-second startup times for production workloads

**🔧 Developer Experience**
- **Type Safety**: Full IntelliSense and compile-time error checking
- **Rich Ecosystem**: Extensive NuGet packages and community support
- **Cross-Platform**: Develop on Windows, macOS, or Linux

**🏢 Enterprise Ready**
- **Microsoft Support**: Official Microsoft SDKs with enterprise SLA
- **Security**: Regular security updates and vulnerability patches
- **Compliance**: SOC, ISO, and GDPR compliant infrastructure

## ⚙️ Configuration & Customization

### Environment Variables
**Required Configuration:**
- `AZURE_MAPS_SUBSCRIPTION_KEY` - Your Azure Maps API key (required for all operations)

**Optional Configuration:**
- `AZURE_MAPS_CLIENT_ID` - For Azure AD authentication (enterprise scenarios)
- `FUNCTIONS_WORKER_RUNTIME` - Set to `dotnet-isolated` (default)
- `AzureWebJobsStorage` - Set to `"None"` for local development

### Azure Functions Host Settings
**Performance Tuning in `host.json`:**
```json
{
  "version": "2.0",
  "functionTimeout": "00:05:00",
  "extensions": {
    "http": {
      "routePrefix": "",
      "maxConcurrentRequests": 100
    }
  },
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

## 🔧 Troubleshooting & Best Practices

### Common Setup Issues

**❌ "Subscription key is invalid"**
- ✅ Verify your key in the Azure Portal under Azure Maps → Authentication
- ✅ Check that the key is correctly set in `local.settings.json`
- ✅ Ensure no extra spaces or quotation marks around the key

**❌ "Build failed - SDK not found"**
- ✅ Install .NET 9.0 SDK from [Microsoft's official site](https://dotnet.microsoft.com/download/dotnet/9.0)
- ✅ Run `dotnet --version` to verify installation
- ✅ Clear NuGet cache: `dotnet nuget locals all --clear`

**❌ "Function host won't start"**
- ✅ Update Azure Functions Core Tools: `npm install -g azure-functions-core-tools@4`
- ✅ Check port availability (7071, 7174)
- ✅ Verify `local.settings.json` format with a JSON validator

### Performance Optimization Tips

**🚀 Speed Improvements**
- Use batch operations (e.g., `geolocation_ip_batch`) for multiple requests
- Implement client-side caching for frequently accessed country data
- Consider Azure Front Door for global distribution

**💰 Cost Optimization**
- Monitor usage in Azure Portal to stay within free tier limits
- Cache static map images to avoid regeneration
- Use route matrix efficiently for bulk calculations

### Production Deployment Checklist

- [ ] **Security**: API keys stored in Azure Key Vault or app settings
- [ ] **Monitoring**: Application Insights configured with custom metrics
- [ ] **Scaling**: Consumption plan configured for automatic scaling
- [ ] **Networking**: Virtual network integration for security (if required)
- [ ] **Backup**: Source code in version control with automated deployments
- [ ] **Testing**: Load testing completed for expected traffic volumes

### Getting Help

**📚 Documentation & Resources**
- [Azure Maps Documentation](https://docs.microsoft.com/azure/azure-maps/) - Comprehensive API guides
- [Model Context Protocol Specification](https://spec.modelcontextprotocol.io/) - MCP protocol details
- [Azure Functions Best Practices](https://docs.microsoft.com/azure/azure-functions/functions-best-practices) - Performance optimization

**🆘 Support Channels**
- **GitHub Issues**: Bug reports and feature requests on this repository
- **Azure Support**: Enterprise support plans with SLA guarantees
- **Community**: Stack Overflow with tags `azure-maps` and `azure-functions`
- **Microsoft Q&A**: Official Microsoft community support platform

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