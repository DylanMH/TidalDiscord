using DiscordRPC;

public class NowPlayingInfo
{
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";

    public DateTime? StartUtc { get; set; }

    public string? ArtworkUrl { get; set; }
    public string? TrackUrl { get; set; }
}


public class DiscordPresenceService : IDisposable
{
    private const string ApplicationId = "1556465962293923921";

    private static readonly TimeSpan ReconnectInterval =
        TimeSpan.FromSeconds(5);

    private DiscordRpcClient? _client;

    private volatile bool _connected;

    private DateTime _lastConnectAttempt = DateTime.MinValue;

    public bool IsConnected => _connected;

    public void EnsureConnected()
    {
        if (_connected &&
            _client is { IsInitialized: true, IsDisposed: false })
        {
            return;
        }

        if (DateTime.UtcNow - _lastConnectAttempt <
            ReconnectInterval)
        {
            return;
        }

        _lastConnectAttempt = DateTime.UtcNow;

        try
        {
            _client?.Dispose();
        }
        catch
        {
            // Old client may already be dead.
        }

        var client = new DiscordRpcClient(ApplicationId)
        {
            SkipIdenticalPresence = true
        };

        client.OnReady += (_, _) =>
        {
            _connected = true;
            AppStatus.SetDiscordConnected(true);
            Logger.Info("Connected to Discord.");
        };

        client.OnClose += (_, e) =>
        {
            MarkDisconnected(
                $"Discord closed the connection ({e.Reason}).");
        };

        client.OnConnectionFailed += (_, _) =>
        {
            MarkDisconnected(
                "Discord connection failed.");
        };

        client.OnError += (_, e) =>
        {
            Logger.Warn(
                $"Discord RPC error {e.Code}: {e.Message}");
        };

        try
        {
            client.Initialize();
            _client = client;
        }
        catch (Exception ex)
        {
            _connected = false;
            AppStatus.SetDiscordConnected(false);
            Logger.Warn(
                $"Could not initialize Discord RPC. {ex.Message}");
        }
    }

    public void SetPresence(
        NowPlayingInfo info,
        bool paused,
        AppSettings settings)
    {
        var client = _client;

        if (!_connected || client == null)
        {
            return;
        }

        string state =
            settings.ShowAlbum &&
            !string.IsNullOrWhiteSpace(info.Album)
                ? $"{info.Artist} • {info.Album}"
                : info.Artist;

        Assets assets;

        if (settings.ShowArtwork &&
            !string.IsNullOrWhiteSpace(info.ArtworkUrl))
        {
            assets = new Assets
            {
                LargeImageKey = info.ArtworkUrl,

                LargeImageText =
                    string.IsNullOrWhiteSpace(info.Album)
                        ? info.Title
                        : info.Album,

                SmallImageKey = "tidal",
                SmallImageText = "TIDAL"
            };
        }
        else
        {
            assets = new Assets
            {
                LargeImageKey = "tidal",
                LargeImageText = "Listening on TIDAL"
            };
        }

        var presence = new RichPresence
        {
            Type = ActivityType.Listening,
            StatusDisplay = StatusDisplayType.Details,

            Details = Limit(info.Title, 128),
            State = Limit(state, 128),

            Timestamps =
                settings.ShowElapsedTime &&
                !paused &&
                info.StartUtc.HasValue
                    ? new Timestamps
                    {
                        Start = info.StartUtc.Value
                    }
                    : null,

            Assets = assets,

            Buttons =
                BuildButtons(info, settings)
        };

        try
        {
            client.SetPresence(presence);
        }
        catch (Exception ex)
        {
            MarkDisconnected(
                $"Failed to set Discord presence. {ex.Message}");
        }
    }

    public void Clear()
    {
        var client = _client;

        if (!_connected || client == null)
        {
            return;
        }

        try
        {
            client.ClearPresence();
        }
        catch (Exception ex)
        {
            MarkDisconnected(
                $"Failed to clear Discord presence. {ex.Message}");
        }
    }

    private void MarkDisconnected(string reason)
    {
        if (_connected)
        {
            Logger.Warn(reason);
        }

        _connected = false;
        AppStatus.SetDiscordConnected(false);
    }

    public void Dispose()
    {
        try
        {
            _client?.ClearPresence();
            _client?.Dispose();
        }
        catch
        {
            // Nothing safe to do during shutdown.
        }

        _connected = false;
        AppStatus.SetDiscordConnected(false);
    }

    // Discord allows at most two buttons. TIDAL first (exact
    // track link when resolved), then Spotify search.
    private static DiscordRPC.Button[]? BuildButtons(
        NowPlayingInfo info,
        AppSettings settings)
    {
        if (!settings.ShowListenButtons)
        {
            return null;
        }

        var buttons = new List<DiscordRPC.Button>(2);

        if (!string.IsNullOrWhiteSpace(info.TrackUrl))
        {
            buttons.Add(
                new DiscordRPC.Button
                {
                    Label = "Listen on TIDAL",
                    Url = info.TrackUrl
                });
        }

        var spotifyUrl =
            SpotifyLinkBuilder.BuildSearchUrl(
                info.Title,
                info.Artist);

        if (spotifyUrl != null && buttons.Count < 2)
        {
            buttons.Add(
                new DiscordRPC.Button
                {
                    Label = "Listen on Spotify",
                    Url = spotifyUrl
                });
        }

        return buttons.Count > 0 ? buttons.ToArray() : null;
    }

    private static string Limit(string text, int maxLength) =>
        text.Length <= maxLength
            ? text
            : text[..(maxLength - 1)] + "…";
}
