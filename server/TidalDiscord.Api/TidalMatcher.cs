using System.Text;
using System.Text.Json;

namespace TidalDiscord.Api;

// Pure matching logic ported from the desktop TidalApiService.
// Operates on the JSON:API searchResults document and picks the
// best track + album artwork. Static and side-effect free so it
// can be unit-tested against fixture payloads.
public static class TidalMatcher
{
    public static TidalMatch? FindBest(
        JsonElement root,
        string title,
        string artist,
        string? album)
    {
        if (!root.TryGetProperty("included", out var included) ||
            included.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        // SearchResults returns its main data as an array.
        if (!root.TryGetProperty("data", out var data))
        {
            return null;
        }

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
                        {
                            trackIds.Add(value);
                        }
                    }
                }
            }
        }

        if (trackIds.Count == 0)
        {
            return null;
        }

        JsonElement? bestTrack = null;
        int bestScore = -1;
        string? bestMatchedArtist = null;

        string wantedTitle = Normalize(title);
        string wantedArtist = Normalize(artist);
        string wantedAlbum =
            string.IsNullOrWhiteSpace(album)
                ? ""
                : Normalize(album);

        foreach (var trackId in trackIds)
        {
            var track =
                FindIncluded(included, "tracks", trackId);

            if (track == null)
            {
                continue;
            }

            int score = 0;
            string? matchedArtist = null;

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
                    {
                        score += 10;
                    }
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

                    var artistDisplayName =
                        artistName.GetString();

                    var candidateArtist =
                        Normalize(artistDisplayName ?? "");

                    if (candidateArtist == wantedArtist)
                    {
                        score += 10;
                        matchedArtist = artistDisplayName;
                        break;
                    }

                    if (candidateArtist.Contains(wantedArtist) ||
                        wantedArtist.Contains(candidateArtist))
                    {
                        score += 5;
                        matchedArtist ??= artistDisplayName;
                    }
                }
            }

            // Small bonus when the caller-supplied album also
            // matches — never outweighs a title/artist match.
            if (!string.IsNullOrEmpty(wantedAlbum))
            {
                var albumTitle =
                    GetAlbumTitle(
                        trackElement, included);

                if (albumTitle != null &&
                    Normalize(albumTitle) == wantedAlbum)
                {
                    score += 4;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestTrack = trackElement;
                bestMatchedArtist = matchedArtist;
            }
        }

        if (bestTrack == null)
        {
            return null;
        }

        // Resolve the album attached to the selected track.
        var albumElement =
            GetAlbumElement(bestTrack.Value, included);

        string? artworkUrl = null;
        string? matchedAlbum = null;

        if (albumElement != null)
        {
            matchedAlbum =
                GetAttribute(albumElement.Value, "title");

            artworkUrl =
                GetBestArtworkUrl(
                    albumElement.Value, included);
        }

        string? selectedTrackId = null;
        string? matchedTitle = null;

        if (bestTrack.Value.TryGetProperty(
                "id",
                out var trackIdElement))
        {
            selectedTrackId = trackIdElement.GetString();
        }

        matchedTitle =
            GetAttribute(bestTrack.Value, "title");

        if (string.IsNullOrWhiteSpace(selectedTrackId))
        {
            return null;
        }

        return new TidalMatch
        {
            TrackId = selectedTrackId,
            ArtworkUrl = artworkUrl,
            MatchedTitle = matchedTitle,
            MatchedArtist = bestMatchedArtist,
            MatchedAlbum = matchedAlbum
        };
    }


    private static JsonElement? GetAlbumElement(
        JsonElement track,
        JsonElement included)
    {
        if (!track.TryGetProperty(
                "relationships",
                out var relationships))
            return null;

        if (!relationships.TryGetProperty(
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
        else if (albumData.ValueKind ==
                     JsonValueKind.Object &&
                 albumData.TryGetProperty("id", out var id))
        {
            albumId = id.GetString();
        }

        if (string.IsNullOrWhiteSpace(albumId))
        {
            return null;
        }

        return FindIncluded(included, "albums", albumId);
    }


    private static string? GetAlbumTitle(
        JsonElement track,
        JsonElement included)
    {
        var album = GetAlbumElement(track, included);

        return album == null
            ? null
            : GetAttribute(album.Value, "title");
    }


    private static string? GetBestArtworkUrl(
        JsonElement album,
        JsonElement included)
    {
        if (!album.TryGetProperty(
                "relationships",
                out var relationships))
            return null;

        if (!relationships.TryGetProperty(
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
        else if (coverArtData.ValueKind ==
                     JsonValueKind.Object &&
                 coverArtData.TryGetProperty("id", out var id))
        {
            artworkId = id.GetString();
        }

        if (string.IsNullOrWhiteSpace(artworkId))
        {
            return null;
        }

        var artworkResource =
            FindIncluded(included, "artworks", artworkId);

        if (artworkResource == null)
        {
            return null;
        }

        if (!artworkResource.Value.TryGetProperty(
                "attributes",
                out var artworkAttributes))
            return null;

        if (!artworkAttributes.TryGetProperty(
                "files",
                out var files))
            return null;

        if (files.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? bestUrl = null;
        long bestPixels = -1;

        // Pick the largest image TIDAL gives us.
        foreach (var file in files.EnumerateArray())
        {
            if (!file.TryGetProperty("href", out var href))
                continue;

            var url = href.GetString();

            if (string.IsNullOrWhiteSpace(url))
                continue;

            long pixels = 0;

            if (file.TryGetProperty("meta", out var meta))
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

        return bestUrl;
    }


    private static string? GetAttribute(
        JsonElement resource,
        string name)
    {
        if (!resource.TryGetProperty(
                "attributes",
                out var attributes))
            return null;

        if (!attributes.TryGetProperty(
                name,
                out var property))
            return null;

        return property.GetString();
    }


    public static JsonElement? FindIncluded(
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


    public static string Normalize(string value)
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

    public static string CacheKey(
        string title,
        string artist,
        string? album) =>
        $"{Normalize(title)}|{Normalize(artist)}|" +
        $"{Normalize(album ?? "")}";
}
