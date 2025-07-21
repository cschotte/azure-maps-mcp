namespace Azure.Maps.Mcp.Services;

public interface IAzureMapsService
{
    Task<string> CallApiAsync(string endpoint, string apiVersion, Dictionary<string, string>? parameters = null);
}