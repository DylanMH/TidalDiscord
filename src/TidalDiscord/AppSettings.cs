using System.Text.Json.Serialization;

public class AppSettings
{
    public bool EnablePresence { get; set; } = true;

    public bool LaunchWithWindows { get; set; }

    public bool HideWhenPaused { get; set; } = true;

    public bool ShowAlbum { get; set; } = true;

    public bool ShowElapsedTime { get; set; } = true;

    public bool ShowArtwork { get; set; } = true;

    public bool ShowListenButtons { get; set; } = true;

    // Legacy settings.json key from before the rename.
    // Read on load so existing users keep their preference;
    // never written back out.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ShowTidalButton
    {
        get => null;
        set
        {
            if (value.HasValue)
            {
                ShowListenButtons = value.Value;
            }
        }
    }
}
