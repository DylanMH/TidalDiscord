using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class TidalApiService
{
    private readonly string _clientId;
    private readonly string _clientSecret;

    private readonly HttpClient _http = new();

    private string? _accessToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;

    public TidalApiService(string clientId, string clientSecret)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    private async Task<string> GetAccessTokenAsync()
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
            await _http.SendAsync(request);

        var json =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"TIDAL authentication failed: " +
                $"{response.StatusCode}\n{json}");
        }

        using var document =
            JsonDocument.Parse(json);

        _accessToken =
            document.RootElement
                .GetProperty("access_token")
                .GetString()
            ?? throw new Exception(
                "TIDAL did not return an access token.");

        var expiresIn =
            document.RootElement
                .GetProperty("expires_in")
                .GetInt32();

        // Refresh five minutes before actual expiry.
        _tokenExpiresAt =
            DateTime.UtcNow.AddSeconds(
                Math.Max(60, expiresIn - 300));

        return _accessToken;
    }

    public async Task<string> SearchAsync(
        string title,
        string artist)
    {
        var token =
            await GetAccessTokenAsync();

        var query =
            $"{title} {artist}";

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
            new HttpRequestMessage(
                HttpMethod.Get,
                url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/vnd.api+json"));

        using var response =
            await _http.SendAsync(request);

        var json =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"TIDAL search failed: " +
                $"{response.StatusCode}\n{json}");
        }

        return json;
    }

    public async Task<TidalTrackInfo?> GetTrackInfoAsync(
        string title,
        string artist)
{
    var json = await SearchAsync(title, artist);

    using var document = JsonDocument.Parse(json);

    var root = document.RootElement;

    if (!root.TryGetProperty("included", out var included) ||
        included.ValueKind != JsonValueKind.Array)
    {
        return null;
    }

    // SearchResults now returns its main data as an array.
    if (!root.TryGetProperty("data", out var data))
        return null;

    var trackIds = new List<string>();

    if (data.ValueKind == JsonValueKind.Array)
    {
        foreach (var searchResult in data.EnumerateArray())
        {
            if (!searchResult.TryGetProperty(
                    "relationships",
                    out var relationships))
                continue;

            if (!relationships.TryGetProperty(
                    "tracks",
                    out var tracksRelationship))
                continue;

            if (!tracksRelationship.TryGetProperty(
                    "data",
                    out var tracks))
                continue;

            if (tracks.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var track in tracks.EnumerateArray())
            {
                if (track.TryGetProperty("id", out var id))
                {
                    var value = id.GetString();

                    if (!string.IsNullOrWhiteSpace(value))
                        trackIds.Add(value);
                }
            }
        }
    }

    if (trackIds.Count == 0)
        return null;

    JsonElement? bestTrack = null;
    int bestScore = -1;

    string wantedTitle = Normalize(title);
    string wantedArtist = Normalize(artist);

    foreach (var trackId in trackIds)
    {
        var track =
            FindIncluded(
                included,
                "tracks",
                trackId);

        if (track == null)
            continue;

        int score = 0;

        var trackElement = track.Value;

        if (trackElement.TryGetProperty(
                "attributes",
                out var attributes))
        {
            if (attributes.TryGetProperty(
                    "title",
                    out var titleProperty))
            {
                var candidateTitle =
                    Normalize(
                        titleProperty.GetString() ?? "");

                if (candidateTitle == wantedTitle)
                    score += 10;
                else if (
                    candidateTitle.Contains(wantedTitle) ||
                    wantedTitle.Contains(candidateTitle))
                {
                    score += 5;
                }
            }
        }

        // Check the artists attached to this track.
        if (trackElement.TryGetProperty(
                "relationships",
                out var relationships) &&
            relationships.TryGetProperty(
                "artists",
                out var artistsRelationship) &&
            artistsRelationship.TryGetProperty(
                "data",
                out var artistData) &&
            artistData.ValueKind == JsonValueKind.Array)
        {
            foreach (var artistIdentifier
                     in artistData.EnumerateArray())
            {
                if (!artistIdentifier.TryGetProperty(
                        "id",
                        out var artistIdElement))
                    continue;

                var artistId =
                    artistIdElement.GetString();

                if (artistId == null)
                    continue;

                var artistResource =
                    FindIncluded(
                        included,
                        "artists",
                        artistId);

                if (artistResource == null)
                    continue;

                if (!artistResource.Value.TryGetProperty(
                        "attributes",
                        out var artistAttributes))
                    continue;

                if (!artistAttributes.TryGetProperty(
                        "name",
                        out var artistName))
                    continue;

                var candidateArtist =
                    Normalize(
                        artistName.GetString() ?? "");

                if (candidateArtist == wantedArtist)
                {
                    score += 10;
                    break;
                }

                if (candidateArtist.Contains(wantedArtist) ||
                    wantedArtist.Contains(candidateArtist))
                {
                    score += 5;
                }
            }
        }

        if (score > bestScore)
        {
            bestScore = score;
            bestTrack = trackElement;
        }
    }

    if (bestTrack == null)
        return null;

    // Find the album attached to the selected track.
    if (!bestTrack.Value.TryGetProperty(
            "relationships",
            out var bestRelationships))
        return null;

    if (!bestRelationships.TryGetProperty(
            "albums",
            out var albumRelationship))
        return null;

    if (!albumRelationship.TryGetProperty(
            "data",
            out var albumData))
        return null;

    string? albumId = null;

    if (albumData.ValueKind == JsonValueKind.Array)
    {
        foreach (var item in albumData.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var id))
            {
                albumId = id.GetString();
                break;
            }
        }
    }
    else if (albumData.ValueKind == JsonValueKind.Object &&
             albumData.TryGetProperty("id", out var id))
    {
        albumId = id.GetString();
    }

    if (string.IsNullOrWhiteSpace(albumId))
        return null;

    var album =
        FindIncluded(
            included,
            "albums",
            albumId);

    if (album == null)
        return null;

    if (!album.Value.TryGetProperty(
            "relationships",
            out var albumRelationships))
        return null;

    if (!albumRelationships.TryGetProperty(
            "coverArt",
            out var coverArtRelationship))
        return null;

    if (!coverArtRelationship.TryGetProperty(
            "data",
            out var coverArtData))
        return null;

    string? artworkId = null;

    if (coverArtData.ValueKind == JsonValueKind.Array)
    {
        foreach (var artwork in coverArtData.EnumerateArray())
        {
            if (artwork.TryGetProperty("id", out var id))
            {
                artworkId = id.GetString();
                break;
            }
        }
    }
    else if (coverArtData.ValueKind == JsonValueKind.Object &&
             coverArtData.TryGetProperty("id", out var id))
    {
        artworkId = id.GetString();
    }

    if (string.IsNullOrWhiteSpace(artworkId))
        return null;

    var artworkResource =
        FindIncluded(
            included,
            "artworks",
            artworkId);

    if (artworkResource == null)
        return null;

    if (!artworkResource.Value.TryGetProperty(
            "attributes",
            out var artworkAttributes))
        return null;

    if (!artworkAttributes.TryGetProperty(
            "files",
            out var files))
        return null;

    if (files.ValueKind != JsonValueKind.Array)
        return null;

    string? bestUrl = null;
    long bestPixels = -1;

    // Pick the largest image TIDAL gives us.
    foreach (var file in files.EnumerateArray())
    {
        if (!file.TryGetProperty(
                "href",
                out var href))
            continue;

        var url = href.GetString();

        if (string.IsNullOrWhiteSpace(url))
            continue;

        long pixels = 0;

        if (file.TryGetProperty(
                "meta",
                out var meta))
        {
            long width = 0;
            long height = 0;

            if (meta.TryGetProperty(
                    "width",
                    out var widthElement))
            {
                widthElement.TryGetInt64(out width);
            }

            if (meta.TryGetProperty(
                    "height",
                    out var heightElement))
            {
                heightElement.TryGetInt64(out height);
            }

            pixels = width * height;
        }

        if (pixels > bestPixels)
        {
            bestPixels = pixels;
            bestUrl = url;
        }
    }

    string? selectedTrackId = null;

    if (bestTrack.Value.TryGetProperty(
        "id",
        out var trackIdElement
    ))
    {
        selectedTrackId = trackIdElement.GetString();
    }

    return new TidalTrackInfo
    {
        TrackId = selectedTrackId,
        ArtworkUrl = bestUrl
    };
}

private static JsonElement? FindIncluded(
    JsonElement included,
    string type,
    string id)
{
    foreach (var resource in included.EnumerateArray())
    {
        if (!resource.TryGetProperty(
                "type",
                out var typeElement))
            continue;

        if (!resource.TryGetProperty(
                "id",
                out var idElement))
            continue;

        if (typeElement.GetString() == type &&
            idElement.GetString() == id)
        {
            return resource;
        }
    }

    return null;
}


private static string Normalize(string value)
{
    var builder = new StringBuilder();

    foreach (char c in value)
    {
        if (char.IsLetterOrDigit(c))
        {
            builder.Append(
                char.ToLowerInvariant(c));
        }
    }

    return builder.ToString();
}
}