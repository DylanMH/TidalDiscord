namespace TidalDiscord.Api;

public record ResolveRequest(
    string? Title,
    string? Artist,
    string? Album);

public record ResolveResponse(
    string TrackId,
    string TrackUrl,
    string? ArtworkUrl,
    string? MatchedTitle,
    string? MatchedArtist,
    string? MatchedAlbum);

public record ErrorResponse(string Error);


public enum ResolveStatus
{
    Found,
    NotFound,
    UpstreamError,
    Unconfigured
}

public record ResolveOutcome(
    ResolveStatus Status,
    TidalMatch? Match,
    bool FromCache = false);


public class TidalMatch
{
    public string? TrackId { get; set; }
    public string? ArtworkUrl { get; set; }
    public string? MatchedTitle { get; set; }
    public string? MatchedArtist { get; set; }
    public string? MatchedAlbum { get; set; }

    public string? TrackUrl =>
        string.IsNullOrWhiteSpace(TrackId)
            ? null
            : $"https://tidal.com/browse/track/{TrackId}";
}


public class TidalUpstreamException : Exception
{
    public TidalUpstreamException(
        string message,
        Exception? inner = null)
        : base(message, inner)
    {
    }
}
