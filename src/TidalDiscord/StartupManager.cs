using Microsoft.Win32;

public static class StartupManager
{
    private const string RunKey =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string ValueName = "TidalDiscord";

    private static string ExecutablePath =>
        Environment.ProcessPath ??
        System.Windows.Forms.Application.ExecutablePath;

    public static bool IsEnabled()
    {
        try
        {
            using var key =
                Registry.CurrentUser.OpenSubKey(RunKey);

            return key?.GetValue(ValueName) != null;
        }
        catch (Exception ex)
        {
            Logger.Warn(
                $"Could not read startup entry. {ex.Message}");
            return false;
        }
    }

    public static void Apply(bool enabled)
    {
        try
        {
            using var key =
                Registry.CurrentUser.CreateSubKey(RunKey);

            if (enabled)
            {
                key.SetValue(
                    ValueName,
                    $"\"{ExecutablePath}\"");
            }
            else
            {
                key.DeleteValue(
                    ValueName,
                    throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(
                "Could not update startup registration.", ex);
        }
    }
}
