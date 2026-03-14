using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Shared.Models;

namespace Client.Services;

public class PromptService
{
    private readonly HttpClient _http;
    private readonly NavigationManager _nav;
    private List<WritingPrompt>? _prompts;
    private readonly Random _random = new();

    public PromptService(HttpClient http, NavigationManager nav)
    {
        _http = http;
        _nav = nav;
    }

    public async Task<WritingPrompt?> GetRandomPromptAsync(
        Register register, Context context, ProficiencyLevel level)
    {
        await EnsureLoadedAsync();
        if (_prompts is null || _prompts.Count == 0) return null;

        var levelName = level.ToString();
        var registerName = register.ToString();
        var contextName = context.ToString();

        var matching = _prompts.Where(p =>
            p.Registers.Contains(registerName, StringComparer.OrdinalIgnoreCase) &&
            p.Contexts.Contains(contextName, StringComparer.OrdinalIgnoreCase) &&
            IsLevelInRange(levelName, p.MinLevel, p.MaxLevel)
        ).ToList();

        // Fall back to level-only match if no exact match
        if (matching.Count == 0)
        {
            matching = _prompts.Where(p =>
                IsLevelInRange(levelName, p.MinLevel, p.MaxLevel)
            ).ToList();
        }

        if (matching.Count == 0) return null;

        return matching[_random.Next(matching.Count)];
    }

    private async Task EnsureLoadedAsync()
    {
        if (_prompts is not null) return;

        try
        {
            // Static assets are served from the app origin, not the API base URL
            using var client = new HttpClient { BaseAddress = new Uri(_nav.BaseUri) };
            _prompts = await client.GetFromJsonAsync<List<WritingPrompt>>("data/writing-prompts.json") ?? [];
        }
        catch
        {
            _prompts = [];
        }
    }

    private static readonly string[] LevelOrder = ["A1", "A2", "B1", "B2", "C1", "C2"];

    private static bool IsLevelInRange(string level, string min, string max)
    {
        var levelIdx = Array.IndexOf(LevelOrder, level);
        var minIdx = Array.IndexOf(LevelOrder, min);
        var maxIdx = Array.IndexOf(LevelOrder, max);
        if (levelIdx < 0 || minIdx < 0 || maxIdx < 0) return false;
        return levelIdx >= minIdx && levelIdx <= maxIdx;
    }
}

public record WritingPrompt
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("text")]
    public string Text { get; init; } = "";

    [JsonPropertyName("contexts")]
    public List<string> Contexts { get; init; } = [];

    [JsonPropertyName("registers")]
    public List<string> Registers { get; init; } = [];

    [JsonPropertyName("minLevel")]
    public string MinLevel { get; init; } = "A1";

    [JsonPropertyName("maxLevel")]
    public string MaxLevel { get; init; } = "C2";
}
