using System.Net.Http.Json;
using Shared.Models;

namespace Client.Services;

public class SessionApiService
{
    private readonly HttpClient _httpClient;

    public SessionApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>Fetch sessions for the current user. Auth is handled by SWA header injection.</summary>
    public async Task<List<SessionDocument>> GetSessionsAsync(int limit = 20)
    {
        var url = $"/api/sessions?limit={limit}";
        return await _httpClient.GetFromJsonAsync<List<SessionDocument>>(url) ?? [];
    }

    /// <summary>Fetch a single session by ID.</summary>
    public async Task<SessionDocument?> GetSessionAsync(string id)
    {
        var url = $"/api/sessions/{Uri.EscapeDataString(id)}";

        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<SessionDocument>();
    }
}
