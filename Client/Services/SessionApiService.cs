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

    public async Task<List<SessionDocument>> GetSessionsAsync(string userId = "anonymous", int limit = 20)
    {
        var url = $"/api/sessions?userId={Uri.EscapeDataString(userId)}&limit={limit}";
        return await _httpClient.GetFromJsonAsync<List<SessionDocument>>(url) ?? [];
    }

    public async Task<SessionDocument?> GetSessionAsync(string id, string userId = "anonymous")
    {
        var url = $"/api/sessions/{Uri.EscapeDataString(id)}?userId={Uri.EscapeDataString(userId)}";
        
        var response = await _httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<SessionDocument>();
    }
}

