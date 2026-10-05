using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

// PUBLIC provider: calls the TidalDiscord metadata backend.
// No credentials required on the client.
public class TidalProxyMetadataProvider : ITrackMetadataProvider
{
    private readonly HttpClient _http;
    private readonly string _resolveUrl;

    public TidalProxyMetadataProvider(
        string baseUrl,
        HttpClient? http = null)
    {
        _resolveUrl =
            baseUrl.TrimEnd('/') + "/v1/resolve";

        _http =
            http ??
            new HttpClient
            {
                // Generous timeout: the hosted backend may be a
                // free-tier instance that needs up to ~60s to
                // wake from idle. Lookups run on a background
                // task, so this never blocks presence updates.
                Timeout = TimeSpan.FromSeconds(60)
            };
    }

    public async Task<TidalTrackInfo?> ResolveAsync(
        string title,
        string artist,
        string? album,
        CancellationToken cancellationToken = default)
    {
        var payload =
            JsonSerializer.Serialize(
                new { title, artist, album });

        using var content =
            new StringContent(
                payload,
                Encoding.UTF8,
                "application/json");

        using var response =
            await _http.PostAsync(
                _resolveUrl,
                content,
                cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        // 429/5xx/other failures throw so the caller can retry
        // with backoff and keep showing basic metadata.
        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        using var document =
            await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

        var root = document.RootElement;

        return new TidalTrackInfo
        {
            TrackId =
                root.TryGetProperty("trackId", out var id)
                    ? id.GetString()
                    : null,

            ArtworkUrl =
                root.TryGetProperty("artworkUrl", out var art)
                    ? art.GetString()
                    : null
        };
    }
}
