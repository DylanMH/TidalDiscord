using Microsoft.Extensions.Caching.Memory;
using TidalDiscord.Api;
using Xunit;

namespace TidalDiscord.Api.Tests;

public class TrackCacheTests
{
    [Fact]
    public void TryGet_ReturnsFalseForMissingKey()
    {
        var cache = new TrackCache(new MemoryCache(
            new MemoryCacheOptions()));

        Assert.False(cache.TryGet("missing", out _));
    }

    [Fact]
    public void StoredMatch_RoundTrips()
    {
        var cache = new TrackCache(new MemoryCache(
            new MemoryCacheOptions()));

        var match = new TidalMatch { TrackId = "42" };
        cache.Set("key", match);

        Assert.True(cache.TryGet("key", out var result));
        Assert.Equal("42", result!.TrackId);
    }

    [Fact]
    public void Miss_IsCachedToo()
    {
        var cache = new TrackCache(new MemoryCache(
            new MemoryCacheOptions()));

        cache.Set("key", null);

        Assert.True(cache.TryGet("key", out var result));
        Assert.Null(result);
    }
}


public class ResolveRequestValidatorTests
{
    [Fact]
    public void NullRequest_IsRejected()
    {
        Assert.NotNull(
            ResolveRequestValidator.Validate(null));
    }

    [Fact]
    public void MissingTitle_IsRejected()
    {
        Assert.NotNull(
            ResolveRequestValidator.Validate(
                new ResolveRequest(null, "Artist", null)));
    }

    [Fact]
    public void MissingArtist_IsRejected()
    {
        Assert.NotNull(
            ResolveRequestValidator.Validate(
                new ResolveRequest("Title", " ", null)));
    }

    [Fact]
    public void OversizedFields_AreRejected()
    {
        var huge = new string('a', 500);

        Assert.NotNull(
            ResolveRequestValidator.Validate(
                new ResolveRequest(huge, "Artist", null)));

        Assert.NotNull(
            ResolveRequestValidator.Validate(
                new ResolveRequest("T", "A", huge)));
    }

    [Fact]
    public void ValidRequest_Passes()
    {
        Assert.Null(
            ResolveRequestValidator.Validate(
                new ResolveRequest(
                    "Glass Houses",
                    "Bad Omens",
                    "THE DEATH OF PEACE OF MIND")));
    }

    [Fact]
    public void ValidRequestWithoutAlbum_Passes()
    {
        Assert.Null(
            ResolveRequestValidator.Validate(
                new ResolveRequest("Song", "Artist", null)));
    }
}
