public static class Logger
{
    private static readonly object _lock = new();

    private static string _logDirectory = "";

    // Avoid filling the log file when something fails every second.
    private static string _lastMessage = "";
    private static DateTime _lastMessageAt = DateTime.MinValue;

    public static void Init()
    {
        _logDirectory =
            Path.Combine(
                SettingsService.DataDirectory,
                "logs");

        try
        {
            Directory.CreateDirectory(_logDirectory);
            PruneOldLogs();
        }
        catch
        {
            // Logging is best-effort.
        }
    }

    public static void Info(string message) =>
        Write("INFO", message);

    public static void Warn(string message) =>
        Write("WARN", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR",
            ex == null
                ? message
                : $"{message} {ex.GetType().Name}: {ex.Message}");

    private static void Write(string level, string message)
    {
        lock (_lock)
        {
            var now = DateTime.Now;

            if (message == _lastMessage &&
                now - _lastMessageAt < TimeSpan.FromSeconds(10))
            {
                return;
            }

            _lastMessage = message;
            _lastMessageAt = now;

            try
            {
                var file =
                    Path.Combine(
                        _logDirectory,
                        $"TidalDiscord-{now:yyyyMMdd}.log");

                File.AppendAllText(
                    file,
                    $"{now:HH:mm:ss} [{level}] {message}\n");
            }
            catch
            {
                // Never let logging break the app.
            }
        }
    }

    private static void PruneOldLogs()
    {
        var cutoff = DateTime.Now.AddDays(-7);

        foreach (var file in
                 Directory.EnumerateFiles(
                     _logDirectory,
                     "TidalDiscord-*.log"))
        {
            try
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
