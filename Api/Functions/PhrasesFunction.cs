using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Api.Services;

namespace Api.Functions;

public class PhrasesFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<PhrasesFunction> _logger;
    private readonly IPhraseRepository _phraseRepo;

    public PhrasesFunction(ILogger<PhrasesFunction> logger, IPhraseRepository phraseRepo)
    {
        _logger = logger;
        _phraseRepo = phraseRepo;
    }

    /// <summary>
    /// GET /api/phrases — list phrases for a user (filterable by level and status)
    /// </summary>
    [Function("GetPhrases")]
    public async Task<IActionResult> GetPhrases(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "phrases")] HttpRequest req)
    {
        var userId = req.Query["userId"].FirstOrDefault() ?? "anonymous";
        var phraseLevel = req.Query["level"].FirstOrDefault();
        var status = req.Query["status"].FirstOrDefault();
        var limitStr = req.Query["limit"].FirstOrDefault();
        var limit = int.TryParse(limitStr, out var l) ? Math.Min(l, 200) : 50;

        _logger.LogInformation("Fetching phrases for user {UserId} (level={Level}, status={Status})",
            userId, phraseLevel ?? "all", status ?? "all");

        try
        {
            var phrases = await _phraseRepo.GetByUserAsync(userId, phraseLevel, status, limit);
            return new OkObjectResult(phrases);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching phrases for user {UserId}", userId);
            return new ObjectResult(new { error = "Failed to load phrases", detail = ex.Message })
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// PATCH /api/phrases/{id} — update phrase status (learning → learned, or vice versa)
    /// </summary>
    [Function("UpdatePhraseStatus")]
    public async Task<IActionResult> UpdatePhraseStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "phrases/{id}")] HttpRequest req,
        string id)
    {
        var userId = req.Query["userId"].FirstOrDefault() ?? "anonymous";

        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var payload = JsonSerializer.Deserialize<PhraseStatusUpdate>(body, JsonOptions);

            if (payload is null || string.IsNullOrWhiteSpace(payload.Status))
                return new BadRequestObjectResult(new { error = "Status is required ('learning' or 'learned')" });

            if (payload.Status is not ("learning" or "learned"))
                return new BadRequestObjectResult(new { error = "Status must be 'learning' or 'learned'" });

            var updated = await _phraseRepo.UpdateStatusAsync(id, userId, payload.Status);

            if (updated is null)
                return new NotFoundObjectResult(new { error = "Phrase not found" });

            return new OkObjectResult(updated);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult(new { error = "Invalid JSON" });
        }
    }

    /// <summary>
    /// POST /api/phrases/{id}/favorite — toggle favorite on a phrase
    /// </summary>
    [Function("TogglePhraseFavorite")]
    public async Task<IActionResult> ToggleFavorite(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "phrases/{id}/favorite")] HttpRequest req,
        string id)
    {
        var userId = req.Query["userId"].FirstOrDefault() ?? "anonymous";

        try
        {
            var updated = await _phraseRepo.ToggleFavoriteAsync(id, userId);

            if (updated is null)
                return new NotFoundObjectResult(new { error = "Phrase not found" });

            return new OkObjectResult(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling favorite on phrase {PhraseId}", id);
            return new ObjectResult(new { error = "Failed to toggle favorite" }) { StatusCode = 500 };
        }
    }

    private record PhraseStatusUpdate
    {
        public string? Status { get; init; }
    }
}

