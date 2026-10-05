// DEVELOPMENT provider: talks to the TIDAL API directly using
// the developer's local User Secrets. Selected only when
// "Tidal:UseDirectApi" is set — public builds use the proxy.
public class TidalDirectMetadataProvider : ITrackMetadataProvider
{
    private readonly TidalApiService _api;

    public TidalDirectMetadataProvider(TidalApiService api)
    {
        _api = api;
    }

    public Task<TidalTrackInfo?> ResolveAsync(
        string title,
        string artist,
        string? album,
        CancellationToken cancellationToken = default) =>
        _api.GetTrackInfoAsync(title, artist);
}
