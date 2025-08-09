// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Maps.Search;
using Azure.Maps.Routing;
using Azure.Maps.Rendering;
using Azure.Maps.Geolocation;
using Microsoft.Extensions.Configuration;

namespace Azure.Maps.Mcp.Services;

public sealed class AzureMapsService : IAzureMapsService
{
    public MapsSearchClient SearchClient { get; }
    public MapsRoutingClient RoutingClient { get; }
    public MapsRenderingClient RenderingClient { get; }
    public MapsGeolocationClient GeolocationClient { get; }

    public AzureMapsService(IConfiguration configuration)
    {
        var subscriptionKey =
            configuration["AZURE_MAPS_SUBSCRIPTION_KEY"] ??
            configuration["Values:AZURE_MAPS_SUBSCRIPTION_KEY"];

        if (string.IsNullOrWhiteSpace(subscriptionKey))
        {
            throw new InvalidOperationException("AZURE_MAPS_SUBSCRIPTION_KEY is required");
        }

        var credential = new AzureKeyCredential(subscriptionKey);
        
        SearchClient = new MapsSearchClient(credential);
        RoutingClient = new MapsRoutingClient(credential);
        RenderingClient = new MapsRenderingClient(credential);
        GeolocationClient = new MapsGeolocationClient(credential);
    }
}