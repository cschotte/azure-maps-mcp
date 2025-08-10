# Azure Maps MCP Server

A Model Context Protocol (MCP) server that exposes common **Azure Maps** capabilities as tools for Large Language Models (LLMs). It’s built on Azure Functions (isolated) and uses the Azure Maps .NET SDK and REST APIs for search, routing, rendering, weather, time zone, and geolocation.

> Important: This MCP server is in preview. Interfaces and behaviors may change. Avoid using in production.

## What you can do

- Geocode and search places (forward and reverse)
- Get administrative boundaries where available
- Plan routes, compute matrices, or reachable ranges (isochrones)
- Render static map images
- Resolve country from IP (single or batch) and validate IPs
- Get time zone info for coordinates
- Get weather conditions, hourly/daily forecasts, and alerts
- Look up country metadata and search by name/code

This project integrates Azure Maps services. Some endpoints use the Azure Maps .NET SDK (Search, Routing, Rendering, Geolocation) and others call Azure Maps REST APIs (e.g., Weather, Time Zone, Snap to Roads). Availability of polygons and certain details can vary by region and data source.

## Tools and when to use them

Each tool accepts a minimal set of parameters focused on common scenarios. Boolean-like tool inputs should be provided as strings: "true" or "false" (case-insensitive).

### Location and country

- location_find
  - What: Geocode an address/place query. Returns coordinates, formatted address, components, confidence. Optionally includes boundary polygons.
  - When: You have text like "Eiffel Tower Paris" or an address and need lat/lon and address parts.
  - Inputs: query (string), maxResults (1–20), includeBoundaries ("true"|"false").
  - Notes: Boundary search attempts the requested type/resolution, then falls back across resolutions and boundary types (locality → postalCode → adminDistrict → countryRegion). Boundaries may not exist for all places.

- location_analyze
  - What: Reverse geocode lat/lon to an address and get boundary polygons for a chosen level.
  - When: You already have coordinates and want address context plus polygons.
  - Inputs: latitude, longitude, boundaryType (locality|postalCode|adminDistrict|countryRegion), resolution (small|medium|large).
  - Notes: Includes the same fallback strategy as above if the specific polygon is unavailable.

- search_country_info
  - What: Get country info by ISO code (alpha‑2 or alpha‑3), e.g., US or USA.
  - When: You want a quick code→name lookup.

- search_countries
  - What: Search countries by name or code.
  - When: You want to match partial names/codes and pick from a list.

### Routing and navigation

- navigation_calculate
  - What: One tool for directions, distance/time matrix, or reachable range.
  - When:
    - directions: turn a list of waypoints into a route.
    - matrix: get all‑to‑all travel times/distances for a set of points.
    - range: compute an isochrone from a center point using time or distance budget.
  - Inputs: coordinates (LatLon[]), calculationType (directions|matrix|range), travelMode, routeType, avoidTolls ("true"|"false"), avoidHighways ("true"|"false"), timeBudgetMinutes or distanceBudgetKm for range.
  - Notes: Uses Azure Maps Routing. Data availability may vary by mode and region.

- navigation_analyze
  - What: Analyze waypoints for international travel (countries traversed, basic considerations).
  - When: High-level travel context is needed in addition to a route.

### Places (POI)

- places_nearby
  - What: Find places near a coordinate with a simple radius.
  - When: You need nearby POIs (e.g., coffee shops around current location).
  - Inputs: latitude, longitude, radiusMeters (100–20000, default 2000), limit (1–25, default 10).

- places_search
  - What: Fuzzy search by keywords with optional lat/lon bias.
  - When: You have a term like "pharmacy 98052" or "bakery" and optional location bias.
  - Inputs: query (string), optional latitude/longitude bias, limit (1–25, default 10).

- places_categories
  - What: Retrieve the POI category tree (IDs and names).
  - When: You need to classify or filter POI searches by categories.

### Maps rendering

- render_staticmap
  - What: Generate a static PNG for a bounding box; supports markers and paths.
  - When: You need to show a simple map snapshot with overlays.
  - Inputs: boundingBox (JSON: {west,south,east,north}), zoomLevel (1–20), width/height (1–8192), mapStyle (road|satellite|hybrid), optional markers/paths.

### Geolocation (IP)

- geolocation_ip
  - What: Resolve a public IPv4/IPv6 to ISO country code (alpha‑2) and country name.
  - When: You need coarse country location for an IP.
  - Notes: Private and loopback IPs aren’t geolocatable.

- geolocation_ip_batch
  - What: Batch variant (up to 100 IPs). Deduplicated and processed in parallel.
  - When: You need to resolve multiple IPs efficiently.

- geolocation_ip_validate
  - What: Validate IP format and indicate public/private, loopback, IPv4/IPv6, and geolocatable.
  - When: You need pre-checks before attempting geolocation.

### Time zones

- timezone_by_coordinates
  - What: Time zone info (standard/daylight offsets, names, sunrise/sunset) for a coordinate.
  - When: You need local time context and offsets for a point.

### Weather

- weather_current
  - What: Current conditions (temperature, wind, humidity, precipitation, etc.).
  - When: You need nowcast-like summary for coordinates.

- weather_hourly
  - What: Hourly forecast for specified hours (subject to SKU availability).
  - When: You need near‑term forecast detail.

- weather_daily
  - What: Daily forecast for a specified number of days (subject to SKU availability).
  - When: You need medium‑range forecast summaries.

- weather_alerts
  - What: Severe weather alerts near a coordinate.
  - When: You need awareness of extreme events impacting the location.

### Roads

- snap_to_roads
  - What: Snap GPS points to the road network; optionally interpolate and include speed limits.
  - When: You need to clean up noisy traces or reason about road alignment.
  - Inputs: points (LatLon[]), includeSpeedLimit ("true"|"false"), interpolate ("true"|"false"), travelMode (driving|truck).

## Inputs and outputs

- Boolean-like parameters must be provided as strings: "true" or "false" (case-insensitive). This avoids function binding issues across different MCP clients.
- Responses have a consistent wrapper: { success, meta, data }. Inside data:
  - List-like responses typically follow { query, items, summary }.
  - Single-result responses follow { query, result }.
  - Some tools add helpful fields (e.g., resolvedType/resolvedResolution for boundaries).

Example MCP call payload (location_find):

```json
{
  "method": "tools/call",
  "params": {
    "name": "location_find",
    "arguments": {
      "query": "Eiffel Tower Paris",
      "maxResults": 5,
      "includeBoundaries": "true"
    }
  }
}
```

## Setup

Prerequisites:
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- An Azure Maps account and subscription key (create in Azure Portal → Azure Maps resource → Authentication)

Local configuration (source/local.settings.json):

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "None",
    "AzureWebJobsSecretStorageType": "Files",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AZURE_MAPS_SUBSCRIPTION_KEY": "<your-azure-maps-subscription-key>"
  }
}
```

Never commit real keys. Use environment variables or Azure Key Vault in production.

## Build and run

Using VS Code tasks:
- build (functions): Clean + build
- func: host start: Start the local Functions host (depends on build)

CLI (optional):
```bash
cd source
dotnet build
cd bin/Debug/net9.0
func host start
```

Local Functions host listens on http://localhost:7071 by default.

## Project layout

```
azure-maps-mcp/
├── source/
│   ├── Program.cs
│   ├── Services/
│   │   ├── IAzureMapsService.cs
│   │   └── AzureMapsService.cs
│   ├── Common/ (helpers for validation, responses, REST client)
│   └── Tools/ (Functions that expose MCP tools)
└── README.md
```

## Notes about Azure Maps (fact-checked at a high level)

- Azure Maps provides REST APIs and SDKs for search/geocoding, routing, rendering, time zone, weather, geolocation, and other geospatial services. Some APIs are available via the Azure Maps .NET SDK used here (Search, Routing, Rendering, Geolocation). Others are accessed via REST in this project (e.g., Weather, Time Zone, Snap to Roads).
- Availability of administrative boundary polygons varies by region and boundary level. A polygon may not be returned for a given type/resolution at specific coordinates.
- Weather and forecast horizons, alert availability, and detail levels depend on data provider coverage and your Azure Maps pricing tier/SKU.

For official details and the latest service capabilities, see Azure Maps documentation: https://learn.microsoft.com/azure/azure-maps/

## Troubleshooting

- Missing key: Ensure AZURE_MAPS_SUBSCRIPTION_KEY is configured.
- Binder errors on booleans: Send "true"/"false" strings for boolean-like tool inputs.
- No boundary geometry: It can be normal; the server tries multiple types/resolutions and returns geometry when available.

## Contributing and support

This project follows the Microsoft Open Source Code of Conduct. See the repository’s CODE_OF_CONDUCT.md.
- Issues and feature requests: open a GitHub issue.
- Azure Maps guidance: https://learn.microsoft.com/azure/azure-maps/
- Commercial support: https://azure.microsoft.com/support/

Trademarks: Use of Microsoft marks is subject to Microsoft’s Trademark & Brand Guidelines.