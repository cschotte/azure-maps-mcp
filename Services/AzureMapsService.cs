using Microsoft.Extensions.Configuration;

namespace Azure.Maps.Mcp.Services;

public class AzureMapsService : IAzureMapsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _subscriptionKey;

    public AzureMapsService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _subscriptionKey = configuration["AZURE_MAPS_SUBSCRIPTION_KEY"]
            ?? throw new InvalidOperationException("Azure Maps subscription key not configured");
    }

    public async Task<string> CallApiAsync(string endpoint, string apiVersion, Dictionary<string, string>? parameters = null)
    {
        var queryParams = new List<string>
        {
            $"api-version={apiVersion}",
            $"subscription-key={_subscriptionKey}"
        };

        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                queryParams.Add($"{param.Key}={Uri.EscapeDataString(param.Value)}");
            }
        }

        var url = $"https://atlas.microsoft.com/{endpoint}?{string.Join("&", queryParams)}";

        using var httpClient = _httpClientFactory.CreateClient("AzureMaps");
        var response = await httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Azure Maps API failed with status {response.StatusCode}: {error}");
        }

        return await response.Content.ReadAsStringAsync();
    }
}