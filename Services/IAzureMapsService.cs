namespace Azure.Maps.Mcp.Services;

public interface IAzureMapsService
{
    Task<string> CallApiAsync(string endpoint, string apiVersion, Dictionary<string, string>? parameters = null);
    Task<ImageResponse> CallImageApiAsync(string endpoint, string apiVersion, Dictionary<string, string>? parameters = null);
}

public record ImageResponse(byte[] Data, string ContentType);