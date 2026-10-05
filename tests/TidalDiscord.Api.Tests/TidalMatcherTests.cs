using System.Text.Json;
using TidalDiscord.Api;
using Xunit;

namespace TidalDiscord.Api.Tests;

public class TidalMatcherTests
{
    [Theory]
    [InlineData("Glass Houses", "glasshouses")]
    [InlineData("THE DEATH OF PEACE OF MIND", "thedeathofpeaceofmind")]
    [InlineData("Song (feat. Someone) [Live]", "songfeatsomeonelive")]
    [InlineData("Björk!", "björk")]
    [InlineData("", "")]
    public void Normalize_StripsNonAlphanumerics(
        string input,
        string expected)
    {
        Assert.Equal(expected, TidalMatcher.Normalize(input));
    }

    [Fact]
    public void CacheKey_IgnoresCaseAndPunctuation()
    {
        var a =
            TidalMatcher.CacheKey(
                "Glass Houses!",
                "bad omens",
                "The Death Of Peace Of Mind");

        var b =
            TidalMatcher.CacheKey(
                "glass houses",
                "BAD OMENS",
                "the death of peace of mind");

        Assert.Equal(a, b);
    }

    [Fact]
    public void CacheKey_DistinguishesAlbums()
    {
        var a = TidalMatcher.CacheKey("Song", "Artist", "A");
        var b = TidalMatcher.CacheKey("Song", "Artist", "B");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void FindBest_ReturnsNull_WhenNoData()
    {
        using var doc =
            JsonDocument.Parse("{\"data\":[],\"included\":[]}");

        var match =
            TidalMatcher.FindBest(
                doc.RootElement,
                "Anything",
                "Anyone",
                null);

        Assert.Null(match);
    }

    [Fact]
    public void FindBest_ReturnsNull_WhenIncludedMissing()
    {
        using var doc =
            JsonDocument.Parse("{\"data\":[{\"a\":1}]}");

        var match =
            TidalMatcher.FindBest(
                doc.RootElement,
                "Anything",
                "Anyone",
                null);

        Assert.Null(match);
    }

    [Fact]
    public void FindBest_PicksCorrectTrackAndLargestArtwork()
    {
        using var doc = JsonDocument.Parse(Fixture);

        var match =
            TidalMatcher.FindBest(
                doc.RootElement,
                "Glass Houses",
                "Bad Omens",
                "THE DEATH OF PEACE OF MIND");

        Assert.NotNull(match);
        Assert.Equal("100", match!.TrackId);
        Assert.Equal(
            "https://tidal.com/browse/track/100",
            match.TrackUrl);
        Assert.Equal(
            "http://img/large.jpg",
            match.ArtworkUrl);
        Assert.Equal("Glass Houses", match.MatchedTitle);
        Assert.Equal("Bad Omens", match.MatchedArtist);
        Assert.Equal(
            "THE DEATH OF PEACE OF MIND",
            match.MatchedAlbum);
    }

    [Fact]
    public void FindBest_WorksWithoutAlbum()
    {
        using var doc = JsonDocument.Parse(Fixture);

        var match =
            TidalMatcher.FindBest(
                doc.RootElement,
                "Glass Houses",
                "Bad Omens",
                null);

        Assert.NotNull(match);
        Assert.Equal("100", match!.TrackId);
    }

    [Fact]
    public void FindBest_FallsBackWhenAlbumMismatch()
    {
        using var doc = JsonDocument.Parse(Fixture);

        // Right title + artist but a different album name still
        // resolves — album is a bonus, not a requirement.
        var match =
            TidalMatcher.FindBest(
                doc.RootElement,
                "Glass Houses",
                "Bad Omens",
                "Some Other Album");

        Assert.NotNull(match);
        Assert.Equal("100", match!.TrackId);
    }

    private const string Fixture = """
        {
          "data": [
            {
              "relationships": {
                "tracks": {
                  "data": [
                    { "id": "100", "type": "tracks" },
                    { "id": "200", "type": "tracks" }
                  ]
                }
              }
            }
          ],
          "included": [
            {
              "type": "tracks", "id": "100",
              "attributes": { "title": "Glass Houses" },
              "relationships": {
                "artists": { "data": [ { "id": "10", "type": "artists" } ] },
                "albums": { "data": [ { "id": "500", "type": "albums" } ] }
              }
            },
            {
              "type": "tracks", "id": "200",
              "attributes": { "title": "Glass Animals Hits" },
              "relationships": {
                "artists": { "data": [ { "id": "11", "type": "artists" } ] },
                "albums": { "data": [ { "id": "600", "type": "albums" } ] }
              }
            },
            {
              "type": "artists", "id": "10",
              "attributes": { "name": "Bad Omens" }
            },
            {
              "type": "artists", "id": "11",
              "attributes": { "name": "Glass Animals" }
            },
            {
              "type": "albums", "id": "500",
              "attributes": { "title": "THE DEATH OF PEACE OF MIND" },
              "relationships": {
                "coverArt": { "data": [ { "id": "900", "type": "artworks" } ] }
              }
            },
            {
              "type": "albums", "id": "600",
              "attributes": { "title": "Dreamland" },
              "relationships": {
                "coverArt": { "data": [ { "id": "901", "type": "artworks" } ] }
              }
            },
            {
              "type": "artworks", "id": "900",
              "attributes": {
                "files": [
                  { "href": "http://img/small.jpg",
                    "meta": { "width": 160, "height": 160 } },
                  { "href": "http://img/large.jpg",
                    "meta": { "width": 1280, "height": 1280 } }
                ]
              }
            },
            {
              "type": "artworks", "id": "901",
              "attributes": {
                "files": [
                  { "href": "http://img/decoy.jpg",
                    "meta": { "width": 640, "height": 640 } }
                ]
              }
            }
          ]
        }
        """;
}
