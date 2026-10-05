using System.Reflection;
using Microsoft.Extensions.Configuration;

// -------------------------------------------------------
// Logging and single-instance guard
// -------------------------------------------------------

Logger.Init();
Logger.Info("TidalDiscord starting.");

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    Logger.Error(
        "Unhandled exception.",
        e.ExceptionObject as Exception);
};

using var instanceMutex =
    new Mutex(
        initiallyOwned: true,
        "TidalDiscord.SingleInstance",
        out bool isNewInstance);

using var showWindowEvent =
    new EventWaitHandle(
        initialState: false,
        EventResetMode.AutoReset,
        "TidalDiscord.ShowStatusWindow");

if (!isNewInstance)
{
    // A copy is already running: ask it to show its window
    // and exit quietly.
    try
    {
        showWindowEvent.Set();
    }
    catch
    {
        // Best effort only.
    }

    return;
}

var showWaiter =
    new Thread(() =>
    {
        while (true)
        {
            try
            {
                showWindowEvent.WaitOne();
                TrayIcon.ShowStatus();
            }
            catch
            {
                return;
            }
        }
    })
    {
        IsBackground = true
    };

showWaiter.Start();


// -------------------------------------------------------
// Settings + startup registration
// -------------------------------------------------------

var settings = SettingsService.Load();
SettingsService.Save();

// Make sure the registry matches the persisted setting.
StartupManager.Apply(settings.LaunchWithWindows);


// -------------------------------------------------------
// TIDAL API credentials (optional)
//
// The app still shows basic Windows media metadata on
// Discord without them; artwork and track links are just
// unavailable. Secrets are stored in .NET User Secrets,
// never in the repo or the published binary.
// -------------------------------------------------------

TidalApiService? tidalApi = null;

try
{
    var config =
        new ConfigurationBuilder()
            .AddUserSecrets(
                Assembly.GetExecutingAssembly(),
                optional: true)
            .Build();

    var clientId = config["Tidal:ClientId"];
    var clientSecret = config["Tidal:ClientSecret"];

    if (!string.IsNullOrWhiteSpace(clientId) &&
        !string.IsNullOrWhiteSpace(clientSecret))
    {
        tidalApi =
            new TidalApiService(clientId, clientSecret);
    }
    else
    {
        Logger.Warn(
            "TIDAL API credentials not configured. " +
            "Artwork and track links will be unavailable.");
    }
}
catch (Exception ex)
{
    Logger.Warn(
        $"Could not load TIDAL credentials. {ex.Message}");
}


// -------------------------------------------------------
// Services
// -------------------------------------------------------

var presence = new DiscordPresenceService();

TrayIcon.ExitRequested += () => presence.Dispose();
TrayIcon.Start();

var media = new TidalMediaService();


// -------------------------------------------------------
// Main loop
// -------------------------------------------------------

string? lastTrackKey = null;
DateTime? lastDiscordStart = null;
string? artworkUrl = null;
string? trackUrl = null;

bool presenceActive = false;
bool lastPausedSent = false;
bool wasDiscordConnected = false;

int lastSettingsVersion = SettingsService.Version;

while (true)
{
    try
    {
        presence.EnsureConnected();

        var snapshot = await media.GetSnapshotAsync();

        // -----------------------------------------------
        // TIDAL not detected
        // -----------------------------------------------

        if (snapshot == null)
        {
            AppStatus.SetTidalDetected(false);
            AppStatus.SetPresenceActive(false);
            AppStatus.UpdateTrack(
                "Nothing playing",
                "",
                "",
                null,
                false,
                TimeSpan.Zero,
                TimeSpan.Zero);

            TrayIcon.SetTooltip("TidalDiscord");

            if (presenceActive)
            {
                presence.Clear();
            }

            presenceActive = false;
            lastTrackKey = null;
            lastDiscordStart = null;
            artworkUrl = null;
            trackUrl = null;

            await Task.Delay(2000);
            continue;
        }

        AppStatus.SetTidalDetected(true);

        var s = SettingsService.Current;

        bool paused = !snapshot.IsPlaying;

        bool showPresence =
            s.EnablePresence &&
            !(paused && s.HideWhenPaused);


        // -----------------------------------------------
        // Track / seek detection
        // -----------------------------------------------

        string trackKey =
            $"{snapshot.Title}|{snapshot.Artist}|{snapshot.Album}";

        bool trackChanged = trackKey != lastTrackKey;

        var discordStart =
            DateTime.UtcNow - snapshot.Position;

        bool seekDetected =
            !paused &&
            lastDiscordStart.HasValue &&
            Math.Abs(
                (discordStart - lastDiscordStart.Value)
                    .TotalSeconds) > 3;

        bool discordReconnected =
            !wasDiscordConnected && presence.IsConnected;

        wasDiscordConnected = presence.IsConnected;


        // -----------------------------------------------
        // TIDAL API lookup on track change only
        // -----------------------------------------------

        if (trackChanged)
        {
            artworkUrl = null;
            trackUrl = null;

            if (tidalApi != null)
            {
                try
                {
                    var info =
                        await tidalApi.GetTrackInfoAsync(
                            snapshot.Title,
                            snapshot.Artist);

                    artworkUrl = info?.ArtworkUrl;
                    trackUrl = info?.TrackUrl;
                }
                catch (Exception ex)
                {
                    Logger.Warn(
                        $"TIDAL track lookup failed. " +
                        $"{ex.Message}");
                }
            }

            lastTrackKey = trackKey;
        }


        // -----------------------------------------------
        // UI status + tray tooltip
        // -----------------------------------------------

        AppStatus.UpdateTrack(
            snapshot.Title,
            snapshot.Artist,
            snapshot.Album,
            artworkUrl,
            snapshot.IsPlaying,
            snapshot.Position,
            snapshot.EndTime);

        TrayIcon.SetTooltip(
            paused
                ? "TidalDiscord"
                : $"TidalDiscord - " +
                  $"{snapshot.Title} - {snapshot.Artist}");


        // -----------------------------------------------
        // Presence disabled or hidden while paused
        // -----------------------------------------------

        if (!showPresence)
        {
            if (presenceActive)
            {
                presence.Clear();
                presenceActive = false;
            }

            AppStatus.SetPresenceActive(false);

            lastDiscordStart = null;
            lastPausedSent = paused;
            lastSettingsVersion = SettingsService.Version;

            await Task.Delay(1000);
            continue;
        }


        // -----------------------------------------------
        // Update Discord only when something changed
        // -----------------------------------------------

        bool pausedChanged = paused != lastPausedSent;

        if (!presenceActive ||
            trackChanged ||
            seekDetected ||
            pausedChanged ||
            discordReconnected ||
            lastSettingsVersion != SettingsService.Version)
        {
            presence.SetPresence(
                new NowPlayingInfo
                {
                    Title = snapshot.Title,
                    Artist = snapshot.Artist,
                    Album = snapshot.Album,
                    StartUtc = discordStart,
                    ArtworkUrl = artworkUrl,
                    TrackUrl = trackUrl
                },
                paused,
                s);

            presenceActive = true;
            lastDiscordStart = discordStart;
            lastPausedSent = paused;
        }

        AppStatus.SetPresenceActive(
            presenceActive && presence.IsConnected);

        lastSettingsVersion = SettingsService.Version;
    }
    catch (Exception ex)
    {
        Logger.Error("Error in main loop.", ex);
    }

    await Task.Delay(1000);
}
