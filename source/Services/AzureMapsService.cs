// Copyright (c) 2025 Clemens Schotte
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Azure.Core;
using Azure.Maps.Search;
using Azure.Maps.Routing;
using Azure.Maps.Rendering;
using Azure.Maps.Geolocation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Maps.Mcp.Services;

/// <summary>
/// Service for Azure Maps operations
/// </summary>
public class AzureMapsService : IAzureMapsService
{
    public MapsSearchClient SearchClient { get; }
    public MapsRoutingClient RoutingClient { get; }
    public MapsRenderingClient RenderingClient { get; }
    public MapsGeolocationClient GeolocationClient { get; }

    public AzureMapsService(IConfiguration configuration, ILogger<AzureMapsService> logger)
    {
        var subscriptionKey = configuration["AZURE_MAPS_SUBSCRIPTION_KEY"];

        if (string.IsNullOrEmpty(subscriptionKey))
        {
            logger.LogError("Azure Maps subscription key not found in configuration");
            throw new InvalidOperationException("Azure Maps subscription key is required");
        }

        logger.LogInformation("Initializing Azure Maps service");
        var credential = new AzureKeyCredential(subscriptionKey);

        SearchClient = new MapsSearchClient(credential);
        RoutingClient = new MapsRoutingClient(credential);
        RenderingClient = new MapsRenderingClient(credential);
        GeolocationClient = new MapsGeolocationClient(credential);
    }
}