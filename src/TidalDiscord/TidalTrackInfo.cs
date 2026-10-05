public class TidalTrackInfo
{
    public string? TrackId { get; set; }

    public string? ArtworkUrl { get; set; }

    public string? TrackUrl =>
        string.IsNullOrWhiteSpace(TrackId)
            ? null
            : $"https://tidal.com/browse/track/{TrackId}";
}