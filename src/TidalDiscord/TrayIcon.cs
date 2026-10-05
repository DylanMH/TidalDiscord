using System.Drawing;
using System.Windows.Forms;

public static class TrayIcon
{
    private static NotifyIcon? _icon;
    private static StatusForm? _statusForm;
    private static Control? _invoker;

    private static ToolStripMenuItem? _presenceItem;
    private static ToolStripMenuItem? _startupItem;

    private static bool _syncingMenu;

    public static event Action? ExitRequested;

    public static void Start()
    {
        var thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "TidalDiscord UI"
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private static void Run()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // A hidden control gives us a handle to marshal work
        // onto this thread's message loop.
        _invoker = new Control();
        _ = _invoker.Handle;

        var menu = new ContextMenuStrip();

        var titleItem = new ToolStripMenuItem("TidalDiscord")
        {
            Enabled = false
        };

        var statusItem =
            new ToolStripMenuItem("Open Status");

        statusItem.Click += (_, _) => ShowStatus();

        _presenceItem =
            new ToolStripMenuItem("Enable Presence")
            {
                CheckOnClick = true,
                Checked =
                    SettingsService.Current.EnablePresence
            };

        _presenceItem.CheckedChanged += (_, _) =>
        {
            if (_syncingMenu)
            {
                return;
            }

            var value = _presenceItem.Checked;

            SettingsService.Update(
                s => s.EnablePresence = value);
        };

        _startupItem =
            new ToolStripMenuItem("Launch with Windows")
            {
                CheckOnClick = true,
                Checked =
                    SettingsService.Current.LaunchWithWindows
            };

        _startupItem.CheckedChanged += (_, _) =>
        {
            if (_syncingMenu)
            {
                return;
            }

            var value = _startupItem.Checked;

            SettingsService.Update(
                s => s.LaunchWithWindows = value);

            StartupManager.Apply(value);
        };

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApplication();

        menu.Items.Add(titleItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(statusItem);
        menu.Items.Add(_presenceItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _icon = new NotifyIcon
        {
            Text = "TidalDiscord",
            Icon = AppIcon.Load(),
            ContextMenuStrip = menu,
            Visible = true
        };

        _icon.DoubleClick += (_, _) => ShowStatus();

        SettingsService.Changed += SyncMenu;

        Application.Run();

        _icon.Visible = false;
        _icon.Dispose();
        _invoker.Dispose();
    }

    public static void ShowStatus()
    {
        Post(() =>
        {
            if (_statusForm == null || _statusForm.IsDisposed)
            {
                _statusForm = new StatusForm();
                _statusForm.Show();
            }
            else
            {
                _statusForm.Show();
                _statusForm.WindowState =
                    FormWindowState.Normal;
                _statusForm.BringToFront();
                _statusForm.Activate();
            }
        });
    }

    public static void SetTooltip(string text)
    {
        // NotifyIcon.Text is limited to 63 characters.
        if (text.Length > 63)
        {
            text = text[..60] + "...";
        }

        Post(() =>
        {
            if (_icon != null)
            {
                _icon.Text = text;
            }
        });
    }

    public static void ExitApplication()
    {
        Post(() =>
        {
            try
            {
                ExitRequested?.Invoke();
            }
            catch
            {
                // Shutdown must not fail on cleanup.
            }

            _icon?.Visible = false;
            Application.Exit();
            Environment.Exit(0);
        });
    }

    private static void SyncMenu()
    {
        Post(() =>
        {
            var s = SettingsService.Current;

            _syncingMenu = true;

            try
            {
                if (_presenceItem != null)
                {
                    _presenceItem.Checked = s.EnablePresence;
                }

                if (_startupItem != null)
                {
                    _startupItem.Checked = s.LaunchWithWindows;
                }
            }
            finally
            {
                _syncingMenu = false;
            }
        });
    }

    private static void Post(Action action)
    {
        var invoker = _invoker;

        if (invoker == null || invoker.IsDisposed)
        {
            return;
        }

        try
        {
            invoker.BeginInvoke(action);
        }
        catch
        {
            // UI thread is shutting down.
        }
    }
}
