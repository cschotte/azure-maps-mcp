// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Azure.Maps.Mcp.Common;

/// <summary>
/// Minimal REST client for Azure Maps REST API. Centralizes base URL, subscription key, and common request patterns.
/// </summary>
public sealed class AtlasRestClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _subscriptionKey;

    public AtlasRestClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _subscriptionKey =
            configuration["AZURE_MAPS_SUBSCRIPTION_KEY"] ??
            configuration["Values:AZURE_MAPS_SUBSCRIPTION_KEY"] ??
            throw new InvalidOperationException("AZURE_MAPS_SUBSCRIPTION_KEY is required for Azure Maps REST calls");
    }

    public async Task<(bool ok, string body, int status, string? reason)> GetAsync(string path, IDictionary<string, string?> query)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://atlas.microsoft.com/");

        // Ensure subscription key always present
        var qp = new List<string>();
        foreach (var kv in query)
        {
            if (!string.IsNullOrWhiteSpace(kv.Value))
                qp.Add($"{kv.Key}={WebUtility.UrlEncode(kv.Value)}");
        }
        qp.Add($"subscription-key={WebUtility.UrlEncode(_subscriptionKey)}");

        var url = path.Contains('?')
            ? $"{path}&{string.Join('&', qp)}"
            : $"{path}?{string.Join('&', qp)}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("Accept", "application/json");

        using var resp = await client.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        return (resp.IsSuccessStatusCode, body, (int)resp.StatusCode, resp.ReasonPhrase);
    }

    public async Task<(bool ok, string body, int status, string? reason)> PostJsonAsync(string path, string json, string contentType = "application/json")
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://atlas.microsoft.com/");

        // Add subscription key to URL
        var url = path.Contains('?')
            ? $"{path}&subscription-key={WebUtility.UrlEncode(_subscriptionKey)}"
            : $"{path}?subscription-key={WebUtility.UrlEncode(_subscriptionKey)}";

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, contentType)
        };
        req.Headers.Add("Accept", "application/json");

        using var resp = await client.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        return (resp.IsSuccessStatusCode, body, (int)resp.StatusCode, resp.ReasonPhrase);
    }
}
