namespace TidalDiscord.Api;

public static class ResolveRequestValidator
{
    public const int MaxTitleLength = 200;
    public const int MaxArtistLength = 200;
    public const int MaxAlbumLength = 300;
    public const long MaxBodyBytes = 4096;

    public static string? Validate(ResolveRequest? request)
    {
        if (request == null)
        {
            return "Request body is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return "\"title\" is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Artist))
        {
            return "\"artist\" is required.";
        }

        if (request.Title.Length > MaxTitleLength ||
            request.Artist.Length > MaxArtistLength ||
            (request.Album?.Length ?? 0) > MaxAlbumLength)
        {
            return "A field exceeds the maximum length.";
        }

        return null;
    }
}
