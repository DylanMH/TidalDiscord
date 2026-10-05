public static class AppConstants
{
    // Single place to switch the metadata backend between
    // localhost development and the production deployment.
#if DEBUG
    public const string DefaultMetadataApiBaseUrl =
        "http://localhost:5087";
#else
    public const string DefaultMetadataApiBaseUrl =
        "https://tidaldiscord.onrender.com";
#endif

    // Optional override usable in any build:
    //   set TIDALDISCORD_API_URL=http://localhost:5087
    public static string MetadataApiBaseUrl =>
        Environment.GetEnvironmentVariable("TIDALDISCORD_API_URL")
            is { Length: > 0 } url
            ? url.TrimEnd('/')
            : DefaultMetadataApiBaseUrl;
}
