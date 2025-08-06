using Azure.Maps.Search;

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
}