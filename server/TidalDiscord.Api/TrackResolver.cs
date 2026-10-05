namespace TidalDiscord.Api;

public class TrackResolver
{
    private readonly TidalClient _tidal;
    private readonly ITrackCache _cache;
    private readonly ILogger<TrackResolver> _logger;

    public TrackResolver(
        TidalClient tidal,
        ITrackCache cache,
        ILogger<TrackResolver> logger)
    {
        _tidal = tidal;
        _cache = cache;
        _logger = logger;
    }

    public bool IsConfigured => _tidal.IsConfigured;

    public async Task<ResolveOutcome> ResolveAsync(
        string title,
        string artist,
        string? album,
        CancellationToken ct)
    {
        var key =
            TidalMatcher.CacheKey(title, artist, album);

        if (_cache.TryGet(key, out var cached))
        {
            return new ResolveOutcome(
                cached == null
                    ? ResolveStatus.NotFound
                    : ResolveStatus.Found,
                cached,
                FromCache: true);
        }

        if (!_tidal.IsConfigured)
        {
            return new ResolveOutcome(
                ResolveStatus.Unconfigured,
                null);
        }

        try
        {
            using var document =
                await _tidal.SearchAsync(title, artist, ct);

            var match =
                TidalMatcher.FindBest(
                    document.RootElement,
                    title,
                    artist,
                    album);

            _cache.Set(key, match);

            return new ResolveOutcome(
                match == null
                    ? ResolveStatus.NotFound
                    : ResolveStatus.Found,
                match);
        }
        catch (TidalUpstreamException ex)
        {
            _logger.LogWarning(
                "TIDAL upstream failure: {Message}",
                ex.Message);

            return new ResolveOutcome(
                ResolveStatus.UpstreamError,
                null);
        }
    }
}
