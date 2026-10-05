public static class AppStatus
{
    private static readonly object _lock = new();

    private static string _title = "Nothing playing";
    private static string _artist = "";
    private static string _album = "";
    private static string? _artworkUrl;

    private static bool _isPlaying;
    private static TimeSpan _position;
    private static TimeSpan _duration;

    private static bool _tidalDetected;
    private static bool _discordConnected;
    private static bool _presenceActive;

    public static void UpdateTrack(
        string title,
        string artist,
        string album,
        string? artworkUrl,
        bool isPlaying,
        TimeSpan position,
        TimeSpan duration)
    {
        lock (_lock)
        {
            _title = title;
            _artist = artist;
            _album = album;
            _artworkUrl = artworkUrl;
            _isPlaying = isPlaying;
            _position = position;
            _duration = duration;
        }
    }

    public static void SetTidalDetected(bool detected)
    {
        lock (_lock)
        {
            _tidalDetected = detected;
        }
    }

    public static void SetDiscordConnected(bool connected)
    {
        lock (_lock)
        {
            _discordConnected = connected;
        }
    }

    public static void SetPresenceActive(bool active)
    {
        lock (_lock)
        {
            _presenceActive = active;
        }
    }

    public static AppStatusSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return new AppStatusSnapshot
            {
                Title = _title,
                Artist = _artist,
                Album = _album,
                ArtworkUrl = _artworkUrl,

                IsPlaying = _isPlaying,
                Position = _position,
                Duration = _duration,

                TidalDetected = _tidalDetected,
                DiscordConnected = _discordConnected,
                PresenceActive = _presenceActive
            };
        }
    }
}


public class AppStatusSnapshot
{
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public string? ArtworkUrl { get; set; }

    public bool IsPlaying { get; set; }
    public TimeSpan Position { get; set; }
    public TimeSpan Duration { get; set; }

    public bool TidalDetected { get; set; }
    public bool DiscordConnected { get; set; }
    public bool PresenceActive { get; set; }
}
