using Windows.Media.Control;

public class TidalMediaService
{
    private const string TidalAppId = "com.squirrel.TIDAL.TIDAL";

    private GlobalSystemMediaTransportControlsSessionManager? _manager;

    public async Task InitializeAsync()
    {
        _manager =
            await GlobalSystemMediaTransportControlsSessionManager
                .RequestAsync();
    }

    public GlobalSystemMediaTransportControlsSession? FindSession()
    {
        if (_manager == null)
        {
            return null;
        }

        foreach (var session in _manager.GetSessions())
        {
            try
            {
                if (session.SourceAppUserModelId.Equals(
                        TidalAppId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return session;
                }
            }
            catch
            {
                // Session may disappear mid-enumeration.
            }
        }

        return null;
    }

    public async Task<MediaSnapshot?> GetSnapshotAsync()
    {
        if (_manager == null)
        {
            try
            {
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                Logger.Warn(
                    $"Could not connect to Windows media " +
                    $"sessions. {ex.Message}");
                return null;
            }
        }

        var session = FindSession();

        if (session == null)
        {
            return null;
        }

        var media =
            await session.TryGetMediaPropertiesAsync();

        var playback =
            session.GetPlaybackInfo();

        var timeline =
            session.GetTimelineProperties();

        bool isPlaying =
            playback.PlaybackStatus ==
            GlobalSystemMediaTransportControlsSessionPlaybackStatus
                .Playing;

        // Windows does not continuously update Position while
        // playing, so extrapolate from the last update time.
        TimeSpan position = timeline.Position;

        if (isPlaying)
        {
            var sinceUpdate =
                DateTimeOffset.UtcNow -
                timeline.LastUpdatedTime;

            if (sinceUpdate > TimeSpan.Zero &&
                sinceUpdate < TimeSpan.FromHours(12))
            {
                position += sinceUpdate;
            }
        }

        if (position < TimeSpan.Zero)
        {
            position = TimeSpan.Zero;
        }

        if (timeline.EndTime > TimeSpan.Zero &&
            position > timeline.EndTime)
        {
            position = timeline.EndTime;
        }

        return new MediaSnapshot
        {
            Title =
                string.IsNullOrWhiteSpace(media.Title)
                    ? "Unknown Track"
                    : media.Title,

            Artist =
                string.IsNullOrWhiteSpace(media.Artist)
                    ? "Unknown Artist"
                    : media.Artist,

            Album = media.AlbumTitle ?? "",

            IsPlaying = isPlaying,
            Position = position,
            EndTime = timeline.EndTime,
            HasThumbnail = media.Thumbnail != null
        };
    }
}


public class MediaSnapshot
{
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";

    public bool IsPlaying { get; set; }

    public TimeSpan Position { get; set; }
    public TimeSpan EndTime { get; set; }

    public bool HasThumbnail { get; set; }
}
