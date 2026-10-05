using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

public class StatusForm : Form
{
    private readonly Label _tidalStatus;
    private readonly Label _discordStatus;
    private readonly Label _presenceStatus;

    private readonly Label _titleLabel;
    private readonly Label _artistLabel;
    private readonly Label _albumLabel;
    private readonly Label _positionLabel;
    private readonly ProgressTrackBar _progressBar;
    private readonly PictureBox _artworkBox;
    private string? _lastArtworkUrl;

    private readonly List<(CheckBox Box, Func<AppSettings, bool> Get)>
        _settingsBoxes = new();

    private bool _syncingSettings;

    private readonly System.Windows.Forms.Timer _refreshTimer;

    public StatusForm()
    {
        Text = "TidalDiscord";
        Icon = AppIcon.Load();

        StartPosition = FormStartPosition.CenterScreen;

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var heading = MakeHeading("TidalDiscord", 15, 16, 12);

        var statusHeading =
            MakeHeading("Connection Status", 10, 16, 46);

        _tidalStatus = MakeStatusLabel(32, 74);
        _discordStatus = MakeStatusLabel(32, 100);
        _presenceStatus = MakeStatusLabel(32, 126);

        var divider1 = MakeDivider(154);

        var nowPlayingHeading =
            MakeHeading("Now Playing", 10, 16, 166);

        _artworkBox = new PictureBox
        {
            Location = new Point(32, 194),
            Size = new Size(72, 72),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle
        };

        _titleLabel = MakeTrackLabel(116, 194, bold: true);
        _artistLabel = MakeTrackLabel(116, 218);
        _albumLabel = MakeTrackLabel(116, 240);
        _albumLabel.ForeColor = Color.DimGray;

        _positionLabel = new Label
        {
            AutoEllipsis = true,
            Location = new Point(116, 258),
            Size = new Size(268, 20),
            ForeColor = Color.DimGray
        };

        _progressBar = new ProgressTrackBar
        {
            Location = new Point(116, 279),
            Size = new Size(268, 14)
        };

        var divider2 = MakeDivider(302);

        var settingsHeading =
            MakeHeading("Settings", 10, 16, 314);

        int checkY = 342;

        MakeCheck(
            "Enable Discord presence",
            s => s.EnablePresence,
            v => SettingsService.Update(
                s => s.EnablePresence = v),
            ref checkY);

        MakeCheck(
            "Hide presence while paused",
            s => s.HideWhenPaused,
            v => SettingsService.Update(
                s => s.HideWhenPaused = v),
            ref checkY);

        MakeCheck(
            "Show album name",
            s => s.ShowAlbum,
            v => SettingsService.Update(
                s => s.ShowAlbum = v),
            ref checkY);

        MakeCheck(
            "Show elapsed time",
            s => s.ShowElapsedTime,
            v => SettingsService.Update(
                s => s.ShowElapsedTime = v),
            ref checkY);

        MakeCheck(
            "Show album artwork",
            s => s.ShowArtwork,
            v => SettingsService.Update(
                s => s.ShowArtwork = v),
            ref checkY);

        MakeCheck(
            "Show \"Listen\" buttons",
            s => s.ShowListenButtons,
            v => SettingsService.Update(
                s => s.ShowListenButtons = v),
            ref checkY);

        MakeCheck(
            "Launch with Windows",
            s => s.LaunchWithWindows,
            v =>
            {
                SettingsService.Update(
                    s => s.LaunchWithWindows = v);
                StartupManager.Apply(v);
            },
            ref checkY);

        var divider3 = MakeDivider(checkY + 4);

        var openTidal = new Button
        {
            Text = "Open TIDAL",
            Location = new Point(16, checkY + 12),
            Size = new Size(116, 30)
        };

        openTidal.Click += (_, _) => OpenTidal();

        var openDiscord = new Button
        {
            Text = "Open Discord",
            Location = new Point(142, checkY + 12),
            Size = new Size(116, 30)
        };

        openDiscord.Click += (_, _) => OpenDiscord();

        var exitButton = new Button
        {
            Text = "Exit",
            Location = new Point(268, checkY + 12),
            Size = new Size(116, 30)
        };

        exitButton.Click += (_, _) =>
            TrayIcon.ExitApplication();

        Controls.AddRange(new Control[]
        {
            heading,
            statusHeading,
            _tidalStatus,
            _discordStatus,
            _presenceStatus,
            divider1,
            nowPlayingHeading,
            _artworkBox,
            _titleLabel,
            _artistLabel,
            _albumLabel,
            _positionLabel,
            _progressBar,
            divider2,
            settingsHeading,
            divider3,
            openTidal,
            openDiscord,
            exitButton
        });

        ClientSize = new Size(400, checkY + 52);

        _refreshTimer =
            new System.Windows.Forms.Timer
            {
                Interval = 1000
            };

        _refreshTimer.Tick += (_, _) => RefreshStatus();
        _refreshTimer.Start();

        SettingsService.Changed += OnSettingsChanged;
        FormClosed += (_, _) =>
        {
            SettingsService.Changed -= OnSettingsChanged;
        };

        RefreshStatus();
    }


    private static Label MakeHeading(
        string text,
        float size,
        int x,
        int y) =>
        new()
        {
            Text = text,
            Font = new Font(
                SystemFonts.DefaultFont.FontFamily,
                size,
                FontStyle.Bold),
            AutoSize = true,
            Location = new Point(x, y)
        };

    private static Label MakeStatusLabel(int x, int y) =>
        new()
        {
            AutoSize = true,
            Location = new Point(x, y)
        };

    private Label MakeTrackLabel(
        int x,
        int y,
        bool bold = false) =>
        new()
        {
            AutoEllipsis = true,
            Location = new Point(x, y),
            Size = new Size(268, 20),
            Font = bold
                ? new Font(
                    Font.FontFamily,
                    10,
                    FontStyle.Bold)
                : Font
        };

    private static Label MakeDivider(int y) =>
        new()
        {
            BorderStyle = BorderStyle.Fixed3D,
            Location = new Point(16, y),
            Size = new Size(368, 2)
        };

    private void MakeCheck(
        string text,
        Func<AppSettings, bool> get,
        Action<bool> apply,
        ref int y)
    {
        var box = new CheckBox
        {
            Text = text,
            AutoSize = true,
            Location = new Point(32, y),
            Checked = get(SettingsService.Current)
        };

        box.CheckedChanged += (_, _) =>
        {
            if (!_syncingSettings)
            {
                apply(box.Checked);
            }
        };

        _settingsBoxes.Add((box, get));
        Controls.Add(box);

        y += 26;
    }

    private void OnSettingsChanged()
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        BeginInvoke(() =>
        {
            if (IsDisposed)
            {
                return;
            }

            _syncingSettings = true;

            try
            {
                var s = SettingsService.Current;

                foreach (var (box, get) in _settingsBoxes)
                {
                    box.Checked = get(s);
                }
            }
            finally
            {
                _syncingSettings = false;
            }
        });
    }

    private static void SetStatus(
        Label label,
        string name,
        string value,
        bool ok,
        bool warn = false)
    {
        label.Text = $"● {name}: {value}";

        label.ForeColor =
            ok ? Color.SeaGreen
            : warn ? Color.Firebrick
            : Color.Gray;
    }


    private void RefreshStatus()
    {
        var status =
            AppStatus.GetSnapshot();

        SetStatus(
            _tidalStatus,
            "TIDAL",
            status.TidalDetected
                ? "Connected"
                : "Not detected",
            status.TidalDetected);

        SetStatus(
            _discordStatus,
            "Discord",
            status.DiscordConnected
                ? "Connected"
                : "Disconnected",
            status.DiscordConnected,
            warn: true);

        SetStatus(
            _presenceStatus,
            "Presence",
            status.PresenceActive
                ? "Active"
                : "Inactive",
            status.PresenceActive);

        _titleLabel.Text = status.Title;
        _artistLabel.Text = status.Artist;
        _albumLabel.Text = status.Album;

        _positionLabel.Text =
            status.TidalDetected &&
            status.Duration > TimeSpan.Zero
                ? $"{status.Position:mm\\:ss} / {status.Duration:mm\\:ss}" +
                  (status.IsPlaying ? "" : "  (Paused)")
                : "";

        _progressBar.SetProgress(
            status.Position,
            status.Duration,
            !status.IsPlaying);

        if (status.ArtworkUrl != _lastArtworkUrl)
        {
            _lastArtworkUrl = status.ArtworkUrl;
            _ = LoadArtworkAsync(status.ArtworkUrl);
        }
    }


    private async Task LoadArtworkAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            _artworkBox.Image = null;
            return;
        }

        var image = await ArtworkCache.GetAsync(url);

        if (IsDisposed)
        {
            return;
        }

        // The image is shared from the cache; do not dispose it.
        _artworkBox.Image = image;
    }


    private static void OpenTidal()
    {
        _ = TryLaunch(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments =
                @"shell:AppsFolder\com.squirrel.TIDAL.TIDAL",
            UseShellExecute = true
        })
        || TryLaunch(
            new ProcessStartInfo("tidal://")
            {
                UseShellExecute = true
            })
        || TryLaunchUrl("https://tidal.com")
        || LogLaunchFailure("TIDAL");
    }

    private static void OpenDiscord()
    {
        var localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        var updateExe =
            Path.Combine(localAppData, @"Discord\Update.exe");

        _ = TryLaunch(
            new ProcessStartInfo("discord://")
            {
                UseShellExecute = true
            })
        || TryLaunch(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments =
                @"shell:AppsFolder\com.squirrel.Discord.Discord",
            UseShellExecute = true
        })
        || (File.Exists(updateExe) &&
            TryLaunch(new ProcessStartInfo
            {
                FileName = updateExe,
                Arguments = "--processStart Discord.exe",
                UseShellExecute = true
            }))
        || TryLaunchUrl("https://discord.com/app")
        || LogLaunchFailure("Discord");
    }

    private static bool TryLaunch(ProcessStartInfo info)
    {
        try
        {
            // With UseShellExecute=true, Process.Start returns
            // null even on success (no handle). An unregistered
            // protocol or missing file throws instead.
            Process.Start(info);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn(
                $"Launch attempt failed ({info.FileName}). " +
                $"{ex.Message}");
            return false;
        }
    }

    private static bool TryLaunchUrl(string url) =>
        TryLaunch(
            new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });

    private static bool LogLaunchFailure(string app)
    {
        Logger.Warn($"Could not launch {app}.");
        return false;
    }


    protected override void OnFormClosing(
        FormClosingEventArgs e)
    {
        // Tray app: closing the window hides it instead of
        // terminating the process.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }
}
