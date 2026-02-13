using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Api.Services;
using System.Text.Json;

namespace Api.Functions;

public class CoachFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<CoachFunction> _logger;
    private readonly ICoachService _coachService;
    private readonly ISessionRepository _sessionRepo;
    private readonly IPhraseRepository _phraseRepo;

    public CoachFunction(
        ILogger<CoachFunction> logger,
        ICoachService coachService,
        ISessionRepository sessionRepo,
        IPhraseRepository phraseRepo)
    {
        _logger = logger;
        _coachService = coachService;
        _sessionRepo = sessionRepo;
        _phraseRepo = phraseRepo;
    }

    [Function("Coach")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "coach")] HttpRequest req)
    {
        _logger.LogInformation("Coach function triggered");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<CoachRequest>(requestBody, JsonOptions);

            if (request is null || string.IsNullOrWhiteSpace(request.Text))
            {
                return new BadRequestObjectResult(new { error = "Text is required" });
            }

            if (request.Text.Length > 5000)
            {
                return new BadRequestObjectResult(new { error = "Text must be 5000 characters or less" });
            }

            if (!Enum.IsDefined(typeof(AIModel), request.Model))
            {
                return new BadRequestObjectResult(new { error = "Invalid AI model selected" });
            }

            var response = await _coachService.AnalyzeAsync(request);

            _ = PersistSessionAsync(request, response);

            return new OkObjectResult(response);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult(new { error = "Invalid JSON in request body" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing coach request");
            return new ObjectResult(new { error = "An error occurred processing your request" })
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Persists the session and extracted phrases in the background.
    /// </summary>
    private async Task PersistSessionAsync(CoachRequest request, CoachResponse response)
    {
        try
        {
            var userId = "anonymous"; // TODO: replace with auth identity

            var session = new SessionDocument
            {
                UserId = userId,
                Request = request,
                Response = response,
                TargetLanguage = request.TargetLanguage,
                TargetLevel = request.TargetLevel.ToString(),
                ModelUsed = response.ModelUsed
            };

            await _sessionRepo.SaveAsync(session);

            if (response.PhraseBank.Count > 0)
            {
                var phrases = response.PhraseBank.Select(p => new PhraseDocument
                {
                    UserId = userId,
                    SourceSessionId = session.Id,
                    Phrase = p.Phrase,
                    Pattern = p.Pattern,
                    Translation = p.Translation,
                    PhraseLevel = p.Level,
                    GrammaticalInfo = p.GrammaticalInfo,
                    Notes = p.Notes,
                    Tags = p.Tags,
                    TargetLanguage = request.TargetLanguage,
                    TargetLevel = request.TargetLevel.ToString()
                }).ToList();

                await _phraseRepo.SaveBatchAsync(phrases);
            }

            _logger.LogInformation("Persisted session {SessionId} with {PhraseCount} phrases",
                session.Id, response.PhraseBank.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist session");
        }
    }
}
