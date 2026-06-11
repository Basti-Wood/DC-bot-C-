using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace DCBot.Services.Roleplay;

public enum GifApiType
{
    Otaku = 0,
    Basti = 1,
    Both = 2,
}

/// <summary>
/// GIF source for the roleplay reactions — port of rp_api.go.
/// Backends: OtakuGIFs (public), Basti API (api.bastiwood.com, needs
/// BASTIAPI key) or Both (random pick per request).
/// </summary>
public sealed class RoleplayApi
{
    private const string BastiBaseUrl = "https://api.bastiwood.com";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly ILogger<RoleplayApi> _logger;
    private readonly Random _random = new();

    public GifApiType ApiType { get; set; } = GifApiType.Otaku;

    public RoleplayApi(ILogger<RoleplayApi> logger)
    {
        _logger = logger;
    }

    public static bool IsBastiAvailable()
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BASTIAPI"));

    private static string GetBastiKey()
        => Environment.GetEnvironmentVariable("BASTIAPI")
           ?? throw new InvalidOperationException("BASTIAPI env var is not set");

    private sealed record GifResponse(string? url);

    public async Task<string?> GetGifUrlAsync(string kind)
    {
        var useBasti = ApiType switch
        {
            GifApiType.Basti => true,
            GifApiType.Both => _random.Next(2) == 1,
            _ => false,
        };

        try
        {
            using var request = useBasti
                ? CreateBastiRequest(HttpMethod.Get, $"/reaction/{Uri.EscapeDataString(kind)}")
                : new HttpRequestMessage(HttpMethod.Get,
                    $"https://api.otakugifs.xyz/gif?reaction={Uri.EscapeDataString(kind)}");

            _logger.LogDebug("Fetching GIF kind={Kind} url={Url}", kind, request.RequestUri);

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetGifUrl: status {Status} for kind={Kind}",
                    (int)response.StatusCode, kind);
                return null;
            }

            var gif = await response.Content.ReadFromJsonAsync<GifResponse>();
            return gif?.url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetGifUrl failed for kind={Kind}", kind);
            return null;
        }
    }

    /// <summary>Store a GIF URL for a reaction in the Basti API (/setgif command).</summary>
    public async Task SetGifUrlAsync(string reaction, string gifUrl)
    {
        using var request = CreateBastiRequest(HttpMethod.Post,
            $"/setreaction/{Uri.EscapeDataString(reaction)}");
        request.Content = JsonContent.Create(new { url = gifUrl });

        using var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage CreateBastiRequest(HttpMethod method, string path)
    {
        var key = GetBastiKey();
        var request = new HttpRequestMessage(method, BastiBaseUrl + path);
        request.Headers.Add("X-API-Key", key);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return request;
    }
}
