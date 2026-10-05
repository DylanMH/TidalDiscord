using System.Diagnostics;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using TidalDiscord.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<TidalClient>();
builder.Services.AddSingleton<TrackResolver>();
builder.Services.AddSingleton<ITrackCache, TrackCache>();

// -------------------------------------------------------
// Rate limiting
//
// Legitimate clients call this endpoint a handful of times
// per hour (once per song change). The per-IP window is far
// above that, plus a small global concurrency cap so we
// can't stampede the TIDAL upstream.
// -------------------------------------------------------

var ratePermit =
    int.TryParse(
        builder.Configuration["RATE_LIMIT_PERMIT"],
        out var p) ? p : 60;

var rateWindowSeconds =
    int.TryParse(
        builder.Configuration["RATE_LIMIT_WINDOW_SECONDS"],
        out var w) ? w : 60;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter =
        PartitionedRateLimiter.Create<HttpContext, string>(
            _ => RateLimitPartition.GetConcurrencyLimiter(
                "global",
                _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = 8,
                    QueueLimit = 4,
                    QueueProcessingOrder =
                        QueueProcessingOrder.OldestFirst
                }));

    options.AddPolicy(
        "resolve",
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress
                    ?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = ratePermit,
                    Window = TimeSpan.FromSeconds(
                        rateWindowSeconds),
                    QueueLimit = 0
                }));
});

var app = builder.Build();

// Behind a reverse proxy / hosted platform, honor
// X-Forwarded-For so rate limiting sees the real client IP.
app.UseForwardedHeaders(
    new ForwardedHeadersOptions
    {
        ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto
    });

app.UseRateLimiter();


// -------------------------------------------------------
// Endpoints
// -------------------------------------------------------

app.MapGet("/health", () =>
    Results.Ok(new { status = "ok" }));

app.MapPost(
        "/v1/resolve",
        async (
            HttpContext http,
            ResolveRequest? request,
            TrackResolver resolver,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger =
                loggerFactory.CreateLogger("Resolve");

            // Reject oversized bodies before doing any work.
            if (http.Request.ContentLength >
                ResolveRequestValidator.MaxBodyBytes)
            {
                return Results.BadRequest(
                    new ErrorResponse(
                        "Request body too large."));
            }

            var validationError =
                ResolveRequestValidator.Validate(request);

            if (validationError != null)
            {
                return Results.BadRequest(
                    new ErrorResponse(validationError));
            }

            var sw = Stopwatch.StartNew();

            var outcome =
                await resolver.ResolveAsync(
                    request!.Title!.Trim(),
                    request.Artist!.Trim(),
                    request.Album?.Trim(),
                    ct);

            sw.Stop();

            // Normal logs carry no track metadata — success,
            // latency, and cache behavior only.
            logger.LogInformation(
                "resolve {Status} cache={CacheHit} ms={Ms}",
                outcome.Status,
                outcome.FromCache,
                sw.ElapsedMilliseconds);

            if (app.Environment.IsDevelopment())
            {
                logger.LogDebug(
                    "resolve meta title={Title} artist={Artist}",
                    request.Title,
                    request.Artist);
            }

            return outcome.Status switch
            {
                ResolveStatus.Found =>
                    Results.Ok(
                        new ResolveResponse(
                            outcome.Match!.TrackId!,
                            outcome.Match.TrackUrl!,
                            outcome.Match.ArtworkUrl,
                            outcome.Match.MatchedTitle,
                            outcome.Match.MatchedArtist,
                            outcome.Match.MatchedAlbum)),

                ResolveStatus.NotFound =>
                    Results.NotFound(
                        new ErrorResponse("not_found")),

                ResolveStatus.Unconfigured =>
                    Results.Problem(
                        statusCode: 503,
                        title:
                            "Metadata service is not configured."),

                _ =>
                    Results.Problem(
                        statusCode: 502,
                        title: "Upstream metadata lookup failed.")
            };
        })
    .RequireRateLimiting("resolve");

app.Run();
