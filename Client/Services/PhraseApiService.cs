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

    /// <summary>Fetch phrases for the current user. Auth is handled by SWA header injection.</summary>
    public async Task<List<PhraseDocument>> GetPhrasesAsync(
        string? phraseLevel = null,
        string? status = null,
        int limit = 50)
    {
        var queryParams = new List<string> { $"limit={limit}" };

        if (!string.IsNullOrEmpty(phraseLevel))
            queryParams.Add($"level={Uri.EscapeDataString(phraseLevel)}");

        if (!string.IsNullOrEmpty(status))
            queryParams.Add($"status={Uri.EscapeDataString(status)}");

        var url = $"/api/phrases?{string.Join("&", queryParams)}";
        return await _httpClient.GetFromJsonAsync<List<PhraseDocument>>(url) ?? [];
    }

    /// <summary>Update a phrase's learning status.</summary>
    public async Task<PhraseDocument?> UpdateStatusAsync(string id, string newStatus)
    {
        var url = $"/api/phrases/{Uri.EscapeDataString(id)}";

        var response = await _httpClient.PatchAsJsonAsync(url, new { status = newStatus });

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<PhraseDocument>();
    }

    /// <summary>Toggle the favorite status of a phrase.</summary>
    public async Task<PhraseDocument?> ToggleFavoriteAsync(string id)
    {
        var url = $"/api/phrases/{Uri.EscapeDataString(id)}/favorite";

        var response = await _httpClient.PostAsync(url, null);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<PhraseDocument>();
    }

    /// <summary>Delete all data for the current user (GDPR).</summary>
    public async Task<bool> DeleteMyDataAsync()
    {
        var response = await _httpClient.DeleteAsync("/api/me/data");
        return response.IsSuccessStatusCode;
    }
}
