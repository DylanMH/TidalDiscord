using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TidalDiscord.Api;

// Talks to the TIDAL API. The client credentials live ONLY in
// server-side configuration (env vars / secrets) and never leave
// this process.
public class TidalClient
{
    private readonly HttpClient _http;
    private readonly string? _clientId;
    private readonly string? _clientSecret;
    private readonly ILogger<TidalClient> _logger;

    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _accessToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;

    public TidalClient(
        IConfiguration config,
        ILogger<TidalClient> logger)
    {
        _logger = logger;

        _clientId =
            config["Tidal:ClientId"] ??
            config["TIDAL_CLIENT_ID"];

        _clientSecret =
            config["Tidal:ClientSecret"] ??
            config["TIDAL_CLIENT_SECRET"];

        _http = new HttpClient(
            new SocketsHttpHandler
            {
                PooledConnectionLifetime =
                    TimeSpan.FromMinutes(10)
            })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_clientId) &&
        !string.IsNullOrWhiteSpace(_clientSecret);

    private async Task<string> GetAccessTokenAsync(
        CancellationToken ct)
    {
        if (_accessToken != null &&
            DateTime.UtcNow < _tokenExpiresAt)
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(ct);

        try
        {
            if (_accessToken != null &&
                DateTime.UtcNow < _tokenExpiresAt)
            {
                return _accessToken;
            }

            var credentials =
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        $"{_clientId}:{_clientSecret}"));

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://auth.tidal.com/v1/oauth2/token");

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Basic",
                    credentials);

            request.Content =
                new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["grant_type"] = "client_credentials"
                    });

            using var response =
                await _http.SendAsync(request, ct);

            var json =
                await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new TidalUpstreamException(
                    $"TIDAL authentication failed: " +
                    $"{(int)response.StatusCode}");
            }

            using var document = JsonDocument.Parse(json);

            _accessToken =
                document.RootElement
                    .GetProperty("access_token")
                    .GetString()
                ?? throw new TidalUpstreamException(
                    "TIDAL did not return an access token.");

            var expiresIn =
                document.RootElement
                    .GetProperty("expires_in")
                    .GetInt32();

            // Refresh five minutes before actual expiry.
            _tokenExpiresAt =
                DateTime.UtcNow.AddSeconds(
                    Math.Max(60, expiresIn - 300));

            _logger.LogInformation(
                "Obtained new TIDAL access token.");

            return _accessToken;
        }
        catch (HttpRequestException ex)
        {
            throw new TidalUpstreamException(
                "TIDAL authentication request failed.", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new TidalUpstreamException(
                "TIDAL authentication timed out.", ex);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public async Task<JsonDocument> SearchAsync(
        string title,
        string artist,
        CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);

        var query = $"{title} {artist}";

        var url =
            "https://openapi.tidal.com/v2/searchResults" +
            "?countryCode=US" +
            "&include=" +
            Uri.EscapeDataString(
                "tracks," +
                "tracks.artists," +
                "tracks.albums," +
                "tracks.albums.coverArt") +
            "&filter%5Bquery%5D=" +
            Uri.EscapeDataString(query);

        using var request =
            new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/vnd.api+json"));

        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new TidalUpstreamException(
                "TIDAL search request failed.", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new TidalUpstreamException(
                "TIDAL search timed out.", ex);
        }

        using (response)
        {
            var json =
                await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new TidalUpstreamException(
                    $"TIDAL search failed: " +
                    $"{(int)response.StatusCode}");
            }

            return JsonDocument.Parse(json);
        }
    }
}
