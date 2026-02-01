using System.Net.Http.Json;
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
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"API request failed: {error}");
        }

        return await response.Content.ReadFromJsonAsync<CoachResponse>();
    }
}

