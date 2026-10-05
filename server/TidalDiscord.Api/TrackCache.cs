using Microsoft.Extensions.Caching.Memory;

namespace TidalDiscord.Api;

public interface ITrackCache
{
    bool TryGet(string key, out TidalMatch? match);
    void Set(string key, TidalMatch? match);
}

// In-memory cache. Swappable for a distributed cache (Redis etc.)
// later via ITrackCache without touching the resolver.
public class TrackCache : ITrackCache
{
    private static readonly TimeSpan HitTtl =
        TimeSpan.FromHours(24);

    private static readonly TimeSpan MissTtl =
        TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;

    public TrackCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool TryGet(string key, out TidalMatch? match)
    {
        return _cache.TryGetValue(key, out match);
    }

    public void Set(string key, TidalMatch? match)
    {
        _cache.Set(
            key,
            match,
            match == null ? MissTtl : HitTtl);
    }
}
