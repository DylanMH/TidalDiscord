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
// Track metadata provider
//
// Public/default mode: the hosted proxy resolves artwork and
// track links — no credentials needed on this machine.
//
// Developer mode: set "Tidal:UseDirectApi" = "true" plus
// Tidal:ClientId / Tidal:ClientSecret in User Secrets to call
// the TIDAL API directly (useful for debugging the matching).
// -------------------------------------------------------

ITrackMetadataProvider? metadataProvider = null;

try
{
    var config =
        new ConfigurationBuilder()
            .AddUserSecrets(
                Assembly.GetExecutingAssembly(),
                optional: true)
            .Build();

    bool useDirect =
        string.Equals(
            config["Tidal:UseDirectApi"],
            "true",
            StringComparison.OrdinalIgnoreCase);

    var clientId = config["Tidal:ClientId"];
    var clientSecret = config["Tidal:ClientSecret"];

    if (useDirect &&
        !string.IsNullOrWhiteSpace(clientId) &&
        !string.IsNullOrWhiteSpace(clientSecret))
    {
        metadataProvider =
            new TidalDirectMetadataProvider(
                new TidalApiService(
                    clientId,
                    clientSecret));

        Logger.Info(
            "Using direct TIDAL API metadata provider.");
    }
    else
    {
        if (useDirect)
        {
            Logger.Warn(
                "Tidal:UseDirectApi is set but TIDAL " +
                "credentials are missing; falling back " +
                "to the proxy provider.");
        }

        metadataProvider =
            new TidalProxyMetadataProvider(
                AppConstants.MetadataApiBaseUrl);

        Logger.Info(
            $"Using proxy metadata provider " +
            $"({AppConstants.MetadataApiBaseUrl}).");
    }

    metadataProvider =
        new CachingMetadataProvider(metadataProvider);
}
catch (Exception ex)
{
    Logger.Warn(
        $"Could not initialize metadata provider. " +
        $"{ex.Message}");
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

// Metadata lookup is retried with backoff on failure so a
// flaky backend doesn't leave a song without artwork, but
// also doesn't get hammered every second.
DateTime nextLookupAttempt = DateTime.MinValue;

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
        // Metadata lookup: on track change, retried with
        // backoff after failures
        // -----------------------------------------------

        if (trackChanged)
        {
            artworkUrl = null;
            trackUrl = null;
            nextLookupAttempt = DateTime.UtcNow;
            lastTrackKey = trackKey;
        }

        if (metadataProvider != null &&
            DateTime.UtcNow >= nextLookupAttempt)
        {
            try
            {
                var info =
                    await metadataProvider.ResolveAsync(
                        snapshot.Title,
                        snapshot.Artist,
                        snapshot.Album);

                artworkUrl = info?.ArtworkUrl;
                trackUrl = info?.TrackUrl;

                nextLookupAttempt = DateTime.MaxValue;
            }
            catch (Exception ex)
            {
                Logger.Warn(
                    $"Track metadata lookup failed. " +
                    $"{ex.Message}");

                nextLookupAttempt =
                    DateTime.UtcNow.AddSeconds(15);
            }
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
