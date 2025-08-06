# Azure Maps MCP Server

A Model Context Protocol (MCP) server implementation that provides Azure Maps functionality as tools for Large Language Models (LLMs). This server exposes Azure Maps Search API capabilities including geocoding, reverse geocoding, and administrative boundary polygon retrieval.

## Overview

This project implements an MCP server using Azure Functions that integrates with Azure Maps services. It allows LLMs to perform geographic operations such as:

- **Geocoding**: Convert addresses or place names to latitude/longitude coordinates
- **Reverse Geocoding**: Convert coordinates to street addresses
- **Polygon Boundaries**: Retrieve administrative boundary polygons for locations (cities, postal codes, states, etc.)

## Features

### 🗺️ Geocoding
Convert street addresses, landmarks, or place names to geographic coordinates with detailed address information including:
- Formatted addresses
- Street numbers and names
- Neighborhoods, postal codes
- Localities and country regions
- Confidence scores and match codes

### 🔄 Reverse Geocoding
Convert latitude/longitude coordinates back to human-readable addresses with comprehensive location details.

### 🌍 Administrative Boundaries
Retrieve polygon boundaries for various administrative levels:
- **Locality** (cities)
- **Postal Code** areas
- **Administrative Districts** (states/provinces, counties)
- **Country Regions**

Support for different resolution levels (small, medium, large) to control polygon detail.

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

Once running, the MCP server exposes the following tools:

### Geocoding
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

**Example**: Geocode "Eiffel Tower, Paris"

### Reverse Geocoding
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

**Example**: Get address for coordinates 48.8584, 2.2945

### Get Polygon
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

**Example**: Get city boundary polygon for Seattle coordinates

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
│       ├── SearchTool.cs          # MCP tools for geocoding and search
│       └── RenderTool.cs          # (Reserved for future rendering tools)
└── README.md
```

## Dependencies

- **Azure.Maps.Search** (2.0.0-beta.5): Azure Maps Search SDK
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
