// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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