public interface ITrackMetadataProvider
{
    // Returns null when no reasonable match exists.
    // Throws for transport/service failures so callers can retry.
    Task<TidalTrackInfo?> ResolveAsync(
        string title,
        string artist,
        string? album,
        CancellationToken cancellationToken = default);
}
