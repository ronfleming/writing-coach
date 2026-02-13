using System.Net.Http.Json;
using Shared.Models;

namespace Client.Services;

public class PhraseApiService
{
    private readonly HttpClient _httpClient;

    public PhraseApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<PhraseDocument>> GetPhrasesAsync(
        string userId = "anonymous",
        string? phraseLevel = null,
        string? status = null,
        int limit = 50)
    {
        var queryParams = new List<string>
        {
            $"userId={Uri.EscapeDataString(userId)}",
            $"limit={limit}"
        };

        if (!string.IsNullOrEmpty(phraseLevel))
            queryParams.Add($"level={Uri.EscapeDataString(phraseLevel)}");

        if (!string.IsNullOrEmpty(status))
            queryParams.Add($"status={Uri.EscapeDataString(status)}");

        var url = $"/api/phrases?{string.Join("&", queryParams)}";
        return await _httpClient.GetFromJsonAsync<List<PhraseDocument>>(url) ?? [];
    }

    public async Task<PhraseDocument?> UpdateStatusAsync(string id, string newStatus, string userId = "anonymous")
    {
        var url = $"/api/phrases/{Uri.EscapeDataString(id)}?userId={Uri.EscapeDataString(userId)}";
        
        var response = await _httpClient.PatchAsJsonAsync(url, new { status = newStatus });
        
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<PhraseDocument>();
    }

    public async Task<PhraseDocument?> ToggleFavoriteAsync(string id, string userId = "anonymous")
    {
        var url = $"/api/phrases/{Uri.EscapeDataString(id)}/favorite?userId={Uri.EscapeDataString(userId)}";

        var response = await _httpClient.PostAsync(url, null);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<PhraseDocument>();
    }
}

