using System.Text.Json;

public static class SettingsService
{
    private static readonly object _lock = new();

    private static AppSettings _current = new();

    private static readonly JsonSerializerOptions JsonOptions =
        new() { WriteIndented = true };

    public static event Action? Changed;

    // Incremented on every change so long-running loops can
    // detect that the settings snapshot they hold is stale.
    public static int Version { get; private set; }

    public static string DataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData),
            "TidalDiscord");

    public static string FilePath =>
        Path.Combine(DataDirectory, "settings.json");

    public static AppSettings Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    public static AppSettings Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);

                    var loaded =
                        JsonSerializer.Deserialize<AppSettings>(
                            json);

                    if (loaded != null)
                    {
                        _current = loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(
                    $"Could not load settings, using defaults. {ex.Message}");
            }

            return _current;
        }
    }

    public static void Update(Action<AppSettings> apply)
    {
        lock (_lock)
        {
            apply(_current);
            Version++;
        }

        Save();
        Changed?.Invoke();
    }

    public static void Save()
    {
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);

                var json =
                    JsonSerializer.Serialize(
                        _current,
                        JsonOptions);

                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Logger.Warn(
                    $"Could not save settings. {ex.Message}");
            }
        }
    }
}
