using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Shared.Models;

namespace Client.Services;

public class CoachApiService
{
    private readonly HttpClient _httpClient;

    public CoachApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CoachResponse?> AnalyzeAsync(CoachRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/coach", request);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var body = await response.Content.ReadFromJsonAsync<RateLimitErrorResponse>();
            throw new RateLimitedException(
                body?.RetryAfterSeconds ?? 60,
                body?.IsAnonymous ?? true,
                body?.Message ?? "Rate limit exceeded");
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"API request failed: {error}");
        }

        return await response.Content.ReadFromJsonAsync<CoachResponse>();
    }

    private record RateLimitErrorResponse
    {
        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }

        [JsonPropertyName("retryAfterSeconds")]
        public int RetryAfterSeconds { get; init; }

        [JsonPropertyName("isAnonymous")]
        public bool IsAnonymous { get; init; }
    }
}

/// <summary>Thrown when the API returns 429 Too Many Requests.</summary>
public class RateLimitedException : Exception
{
    public int RetryAfterSeconds { get; }
    public bool IsAnonymous { get; }

    public RateLimitedException(int retryAfterSeconds, bool isAnonymous, string message)
        : base(message)
    {
        RetryAfterSeconds = retryAfterSeconds;
        IsAnonymous = isAnonymous;
    }
}
