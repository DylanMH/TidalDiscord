using System.Text;

// Small local cache in front of whichever metadata provider is
// active so repeat plays don't hit the backend/TIDAL again.
// Bounded: successes cached until evicted, misses for a few
// minutes.
public class CachingMetadataProvider : ITrackMetadataProvider
{
    private const int MaxEntries = 256;

    private static readonly TimeSpan MissTtl =
        TimeSpan.FromMinutes(5);

    private readonly ITrackMetadataProvider _inner;
    private readonly object _lock = new();

    private readonly Dictionary<
        string,
        (TidalTrackInfo? Info, DateTime MissUntil)> _cache = new();

    public CachingMetadataProvider(ITrackMetadataProvider inner)
    {
        _inner = inner;
    }

    public async Task<TidalTrackInfo?> ResolveAsync(
        string title,
        string artist,
        string? album,
        CancellationToken cancellationToken = default)
    {
        var key =
            $"{Normalize(title)}|{Normalize(artist)}|" +
            $"{Normalize(album ?? "")}";

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.Info != null)
                {
                    return entry.Info;
                }

                if (entry.MissUntil > DateTime.UtcNow)
                {
                    return null;
                }
            }
        }

        var info =
            await _inner.ResolveAsync(
                title,
                artist,
                album,
                cancellationToken);

        lock (_lock)
        {
            if (_cache.Count >= MaxEntries)
            {
                _cache.Clear();
            }

            _cache[key] =
                (info,
                 info == null
                     ? DateTime.UtcNow + MissTtl
                     : DateTime.MaxValue);
        }

        return info;
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder();

        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }
}
