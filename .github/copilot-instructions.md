## Azure Maps MCP – working notes for AI coding agents

This repo is a .NET 9 Azure Functions (isolated) MCP server exposing Azure Maps as MCP tools. Follow these project‑specific patterns.

### Architecture (what lives where)
- `source/Program.cs`: boot Functions isolated host, enable MCP metadata, register DI.
- `source/Services/*`: `IAzureMapsService` + `AzureMapsService` create SDK clients using `AZURE_MAPS_SUBSCRIPTION_KEY`.
- `source/Common/*`: cross‑cutting helpers: `ValidationHelper`, `ResponseHelper`, `AtlasRestClient`, `ToolsHelper`, `Models/LatLon.cs`.
- `source/Tools/*`: each file is a tool surface (Location, Navigation, Places, Render, Geolocation, TimeZone, Weather, SnapToRoads, Country).
- Integration paths: SDK (Search/Routing/Rendering/Geolocation) vs REST (Weather/TimeZone/SnapToRoads/Places via `AtlasRestClient`).

### Contracts and conventions
- Always return via `BaseMapsTool.ExecuteWithErrorHandling(...)` → `{ success, meta:{requestId,timestamp}, data|error }`.
- Validate inputs with `ValidationHelper` before SDK/REST calls (coords, ranges, IPs, arrays, enum/boolean strings).
- Boolean parameters: some are `string` (send "true"/"false"; e.g., `includeBoundaries`, `avoidTolls/avoidHighways`), others are real `bool` (e.g., TimeZone/Weather/SnapToRoads). Match the signature.
- Coordinate order: SDK `GeoPosition(longitude, latitude)` vs public `LatLon { Latitude, Longitude }`.

### Build/run locally
- Tasks: "build (functions)" then "func: host start" (cwd `source/bin/Debug/net9.0`).
- CLI: `dotnet build` in `source`, then run Functions host from `source/bin/Debug/net9.0` (http://localhost:7071).
- Configure `AZURE_MAPS_SUBSCRIPTION_KEY` in `source/local.settings.json` → `Values`.

### Tool implementation pattern
- Derive from `BaseMapsTool`; inject services (IAzureMapsService, AtlasRestClient, CountryHelper, ILogger<T>). 
- Annotate a method with `[Function(nameof(...))]` and `[McpToolTrigger("tool_name", "desc")]`; annotate parameters with `[McpToolProperty(...)]`.
- Shape outputs like: `{ query, result }` or `{ query, items, summary }`. Keep results compact.
- Reuse proven flows: boundary fallback (`LocationTool.GetLocationBoundary`); route multiplexing (`NavigationTool.CalculateRoute`).

### REST usage (Azure Maps)
- Use `AtlasRestClient.GetAsync/PostJsonAsync`; it injects `subscription-key` and base URL. 
- Prefer simplified responses via `System.Text.Json` (see `WeatherTool`, `PlacesTool`). Limit payloads or include counts.

### Pitfalls
- Don’t swap lat/lon when building `GeoPosition` or converting geometry.
- Only use `ValidateBooleanString` for parameters declared as `string` booleans.
- Log meaningful info via `ILogger`; the response wrapper adds `requestId` in `meta`.

Examples: SDK (`Tools/NavigationTool.cs`, `RenderTool.cs`, `LocationTool.cs`), REST (`WeatherTool.cs`, `TimeZoneTool.cs`, `PlacesTool.cs`, `SnapToRoadsTool.cs`), helpers (`Common/*`), wiring (`Services/*`).

If anything here seems out of date, update this file with the minimal, factual delta.
