using System.Collections.Concurrent;
using System.Drawing;

public static class ArtworkCache
{
    private const int MaxEntries = 32;

    private static readonly HttpClient Http = new();

    private static readonly ConcurrentDictionary<
        string, Task<Image?>> _cache = new();

    // The returned image is shared. Callers must NOT dispose it.
    public static async Task<Image?> GetAsync(string url)
    {
        if (_cache.TryGetValue(url, out var existing))
        {
            return await existing;
        }

        var task = LoadAsync(url);
        _cache[url] = task;

        var image = await task;

        if (image == null)
        {
            // Transient failures should be retried later.
            _cache.TryRemove(url, out _);
        }
        else if (_cache.Count > MaxEntries)
        {
            _cache.Clear();
            _cache[url] = task;
        }

        return image;
    }

    private static async Task<Image?> LoadAsync(string url)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(url);

            using var stream = new MemoryStream(bytes);
            using var downloaded = Image.FromStream(stream);

            return new Bitmap(downloaded);
        }
        catch (Exception ex)
        {
            Logger.Warn(
                $"Could not download artwork. {ex.Message}");
            return null;
        }
    }
}
