using Azure.Core;
using Azure.Maps.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Maps.Mcp.Services;

/// <summary>
/// Service for Azure Maps operations
/// </summary>
public class AzureMapsService : IAzureMapsService
{
    public MapsSearchClient SearchClient { get; }

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
    }
}