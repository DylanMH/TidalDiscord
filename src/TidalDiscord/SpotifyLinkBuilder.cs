// Builds Spotify links locally from track metadata.
// No Spotify API credentials are needed — this produces a
// plain search URL that opens Spotify's own search page.
public static class SpotifyLinkBuilder
{
    public static string? BuildSearchUrl(
        string? title,
        string? artist)
    {
        var query =
            string.Join(
                " ",
                new[] { title, artist }
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!.Trim()));

        if (query.Length == 0)
        {
            return null;
        }

        return
            "https://open.spotify.com/search/" +
            Uri.EscapeDataString(query);
    }
}
