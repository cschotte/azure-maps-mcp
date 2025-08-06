// Copyright (c) 2025 Clemens Schotte
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Azure.Maps.Search;
using Azure.Maps.Routing;
using Azure.Maps.Rendering;
using Azure.Maps.Geolocation;

namespace Azure.Maps.Mcp.Services;

/// <summary>
/// Service interface for Azure Maps operations
/// </summary>
public interface IAzureMapsService
{
    /// <summary>
    /// Gets the configured Azure Maps Search client
    /// </summary>
    MapsSearchClient SearchClient { get; }

    /// <summary>
    /// Gets the configured Azure Maps Routing client
    /// </summary>
    MapsRoutingClient RoutingClient { get; }

    /// <summary>
    /// Gets the configured Azure Maps Rendering client
    /// </summary>
    MapsRenderingClient RenderingClient { get; }

    /// <summary>
    /// Gets the configured Azure Maps Geolocation client
    /// </summary>
    MapsGeolocationClient GeolocationClient { get; }
}