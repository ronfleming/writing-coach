using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Client.Services;

/// <summary>
/// Client-side auth service that reads identity from Azure Static Web Apps'
/// /.auth/me endpoint. Caches the result for the lifetime of the session.
/// In local dev, /.auth/me doesn't exist → gracefully falls back to anonymous.
/// </summary>
public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly bool _isDevelopment;
    private AuthState? _cachedState;

    public AuthService(IWebAssemblyHostEnvironment hostEnv)
    {
        // Use the app's own origin (not the API base) — /.auth/me is served by SWA
        _httpClient = new HttpClient { BaseAddress = new Uri(hostEnv.BaseAddress) };
        _isDevelopment = hostEnv.IsDevelopment();
    }

    public bool IsAuthenticated => _cachedState?.IsAuthenticated ?? false;
    public string CurrentUserId => _cachedState?.UserId ?? "anonymous";
    public string CurrentUserDisplayName => _cachedState?.DisplayName ?? "Anonymous";
    public string? IdentityProvider => _cachedState?.IdentityProvider;
    public AuthState State => _cachedState ?? AuthState.Anonymous;

    /// <summary>
    /// Fetch auth state from SWA. Results are cached — pass forceRefresh to re-fetch.
    /// </summary>
    public async Task<AuthState> GetAuthStateAsync(bool forceRefresh = false)
    {
        if (_cachedState is not null && !forceRefresh)
            return _cachedState;

        try
        {
            var response = await _httpClient.GetAsync(".auth/me");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AuthMeResponse>();
                var principal = result?.ClientPrincipal;

                if (principal is not null &&
                    principal.UserRoles?.Contains("authenticated") == true &&
                    !string.IsNullOrEmpty(principal.UserId))
                {
                    _cachedState = new AuthState(
                        true,
                        principal.UserId,
                        principal.UserDetails ?? principal.UserId,
                        principal.IdentityProvider,
                        principal.UserRoles ?? ["authenticated"]);
                    return _cachedState;
                }
            }
        }
        catch
        {
            // Expected in local dev — /.auth/me doesn't exist
        }

        // In local dev, simulate an authenticated user so the full pipeline is testable
        if (_isDevelopment)
        {
            _cachedState = new AuthState(
                true, "dev-user", "Developer", "dev", ["anonymous", "authenticated"]);
            return _cachedState;
        }

        _cachedState = AuthState.Anonymous;
        return _cachedState;
    }

    /// <summary>Clear cached state (call after login/logout navigation).</summary>
    public void ClearCache() => _cachedState = null;

    // ── SWA response models ──────────────────────────────────────────

    private record AuthMeResponse
    {
        [JsonPropertyName("clientPrincipal")]
        public ClientPrincipalResponse? ClientPrincipal { get; init; }
    }

    private record ClientPrincipalResponse
    {
        [JsonPropertyName("identityProvider")]
        public string? IdentityProvider { get; init; }

        [JsonPropertyName("userId")]
        public string? UserId { get; init; }

        [JsonPropertyName("userDetails")]
        public string? UserDetails { get; init; }

        [JsonPropertyName("userRoles")]
        public List<string>? UserRoles { get; init; }
    }
}

public record AuthState(
    bool IsAuthenticated,
    string UserId,
    string DisplayName,
    string? IdentityProvider,
    List<string> Roles)
{
    public static readonly AuthState Anonymous = new(
        false, "anonymous", "Anonymous", null, ["anonymous"]);

    public bool IsAdmin => Roles.Contains("admin");
}

