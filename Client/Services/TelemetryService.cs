using Microsoft.JSInterop;

namespace Client.Services;

/// <summary>
/// Thin wrapper around the Application Insights JavaScript SDK.
/// Call InitializeAsync once on app startup — it's a no-op if the
/// connection string isn't configured.
/// </summary>
public class TelemetryService
{
    private readonly IJSRuntime _js;
    private bool _initialized;

    public TelemetryService(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>
    /// Initialize App Insights with the given connection string.
    /// Pass null or empty to skip (e.g., in local dev).
    /// </summary>
    public async Task InitializeAsync(string? connectionString)
    {
        if (_initialized || string.IsNullOrEmpty(connectionString))
            return;

        try
        {
            await _js.InvokeVoidAsync("initAppInsights", connectionString);
            _initialized = true;
        }
        catch
        {
            // Telemetry should never break the app
        }
    }

    /// <summary>Track a custom event (e.g., "CoachingSubmitted", "PhraseFavorited").</summary>
    public async Task TrackEventAsync(string eventName, Dictionary<string, string>? properties = null)
    {
        if (!_initialized) return;

        try
        {
            await _js.InvokeVoidAsync("trackEvent", eventName, properties ?? new());
        }
        catch
        {
            // Swallow — telemetry must not break UX
        }
    }

    /// <summary>Track a page view.</summary>
    public async Task TrackPageViewAsync(string pageName)
    {
        if (!_initialized) return;

        try
        {
            await _js.InvokeVoidAsync("trackPageView", pageName);
        }
        catch { }
    }

    /// <summary>Set the authenticated user context for correlating telemetry.</summary>
    public async Task SetAuthenticatedUserAsync(string userId)
    {
        if (!_initialized) return;

        try
        {
            await _js.InvokeVoidAsync("setAuthenticatedUser", userId);
        }
        catch { }
    }
}

