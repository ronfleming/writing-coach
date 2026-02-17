using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace Api.Helpers;

/// <summary>
/// Decodes the x-ms-client-principal header injected by Azure Static Web Apps.
/// Returns "anonymous" when no valid principal is present (dev or unauthenticated).
/// In local dev (AZURE_FUNCTIONS_ENVIRONMENT=Development), "anonymous" is treated
/// as a valid user so the full persistence pipeline works without real auth.
/// </summary>
public static class AuthHelper
{
    private const string ClientPrincipalHeader = "x-ms-client-principal";

    /// <summary>True when running locally via <c>func start</c>.</summary>
    public static bool IsDevelopment =>
        string.Equals(
            Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT"),
            "Development",
            StringComparison.OrdinalIgnoreCase);

    public static string GetUserId(HttpRequest req)
    {
        var principal = Parse(req);
        return principal?.IsAuthenticated == true ? principal.UserId : "anonymous";
    }

    public static bool IsAuthenticated(HttpRequest req)
    {
        var principal = Parse(req);
        return principal?.IsAuthenticated == true;
    }

    public static bool IsAdmin(HttpRequest req)
    {
        var principal = Parse(req);
        return principal?.UserRoles?.Contains("admin") == true;
    }

    public static ClientPrincipal? Parse(HttpRequest req)
    {
        if (!req.Headers.TryGetValue(ClientPrincipalHeader, out var headerValue))
            return null;

        var encoded = headerValue.FirstOrDefault();
        if (string.IsNullOrEmpty(encoded))
            return null;

        try
        {
            var decoded = Convert.FromBase64String(encoded);
            var json = Encoding.UTF8.GetString(decoded);
            return JsonSerializer.Deserialize<ClientPrincipal>(json);
        }
        catch
        {
            return null;
        }
    }

    public static string GetClientIp(HttpRequest req)
    {
        // Azure SWA / Front Door uses X-Forwarded-For
        var forwarded = req.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
            return forwarded.Split(',')[0].Trim();

        return req.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

public record ClientPrincipal
{
    [JsonPropertyName("identityProvider")]
    public string IdentityProvider { get; init; } = "";

    [JsonPropertyName("userId")]
    public string UserId { get; init; } = "";

    [JsonPropertyName("userDetails")]
    public string UserDetails { get; init; } = "";

    [JsonPropertyName("userRoles")]
    public List<string> UserRoles { get; init; } = [];

    public bool IsAuthenticated => UserRoles.Contains("authenticated");
}

