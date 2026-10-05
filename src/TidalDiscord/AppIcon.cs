using System.Drawing;
using System.Reflection;

public static class AppIcon
{
    private static Icon? _icon;

    public static Icon Load()
    {
        if (_icon != null)
        {
            return _icon;
        }

        try
        {
            using var stream =
                Assembly
                    .GetExecutingAssembly()
                    .GetManifestResourceStream(
                        "TidalDiscord.ico");

            if (stream != null)
            {
                _icon = new Icon(stream);
                return _icon;
            }
        }
        catch
        {
            // Fall through to other sources.
        }

        try
        {
            var path = Environment.ProcessPath;

            if (path != null)
            {
                var extracted =
                    Icon.ExtractAssociatedIcon(path);

                if (extracted != null)
                {
                    _icon = extracted;
                    return _icon;
                }
            }
        }
        catch
        {
            // Fall through to the default icon.
        }

        _icon = SystemIcons.Application;
        return _icon;
    }
}
