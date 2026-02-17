using System.Collections.Concurrent;

namespace Api.Services;

/// <summary>
/// In-memory sliding-window rate limiter. Tracks request timestamps per key
/// (IP for anonymous, userId for authenticated). Single-instance only — acceptable
/// for Azure Functions consumption plan where most traffic hits one instance.
/// </summary>
public class RateLimitService
{
    private static readonly ConcurrentDictionary<string, List<DateTimeOffset>> Requests = new();
    private static DateTimeOffset _lastCleanup = DateTimeOffset.UtcNow;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(10);

    public const int AnonymousLimitPerHour = 5;
    public const int AuthenticatedLimitPerHour = 20;
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    /// <summary>Known bot user-agent substrings (lowercase).</summary>
    private static readonly string[] BotPatterns =
    [
        "python-requests", "python-urllib", "curl/", "wget/",
        "scrapy", "bot", "spider", "crawler", "headlesschrome",
        "phantomjs", "selenium", "puppeteer", "playwright",
        "httpclient", "java/", "go-http-client", "node-fetch",
        "axios/", "postman"
    ];

    /// <summary>
    /// Check whether a request should be rate-limited.
    /// </summary>
    /// <param name="key">IP address (anon) or userId (authenticated).</param>
    /// <param name="isAuthenticated">True → higher limit.</param>
    public RateLimitResult Check(string key, bool isAuthenticated)
    {
        CleanupIfNeeded();

        var now = DateTimeOffset.UtcNow;
        var limit = isAuthenticated ? AuthenticatedLimitPerHour : AnonymousLimitPerHour;
        var windowStart = now - Window;

        var timestamps = Requests.GetOrAdd(key, _ => new List<DateTimeOffset>());

        lock (timestamps)
        {
            timestamps.RemoveAll(t => t < windowStart);

            if (timestamps.Count >= limit)
            {
                var oldest = timestamps.Min();
                var retryAfter = (int)Math.Ceiling((oldest + Window - now).TotalSeconds);
                return new RateLimitResult(true, Math.Max(retryAfter, 1), limit, timestamps.Count);
            }

            timestamps.Add(now);
            return new RateLimitResult(false, 0, limit, timestamps.Count);
        }
    }

    /// <summary>Returns true if the user-agent looks like a bot or is missing.</summary>
    public static bool IsBotUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return true;

        var ua = userAgent.ToLowerInvariant();
        return BotPatterns.Any(pattern => ua.Contains(pattern));
    }

    private static void CleanupIfNeeded()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastCleanup < CleanupInterval) return;
        _lastCleanup = now;

        var cutoff = now - Window;
        var keysToRemove = new List<string>();

        foreach (var kvp in Requests)
        {
            lock (kvp.Value)
            {
                kvp.Value.RemoveAll(t => t < cutoff);
                if (kvp.Value.Count == 0)
                    keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
            Requests.TryRemove(key, out _);
    }
}

/// <param name="IsLimited">True if the request should be rejected.</param>
/// <param name="RetryAfterSeconds">Seconds until the client can retry.</param>
/// <param name="Limit">The applicable limit for this caller.</param>
/// <param name="CurrentCount">Requests already used in the current window.</param>
public record RateLimitResult(bool IsLimited, int RetryAfterSeconds, int Limit, int CurrentCount);

