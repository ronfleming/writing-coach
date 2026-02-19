using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Api.Services;
using Api.Helpers;
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
    private readonly RateLimitService _rateLimiter;

    public CoachFunction(
        ILogger<CoachFunction> logger,
        ICoachService coachService,
        ISessionRepository sessionRepo,
        IPhraseRepository phraseRepo,
        RateLimitService rateLimiter)
    {
        _logger = logger;
        _coachService = coachService;
        _sessionRepo = sessionRepo;
        _phraseRepo = phraseRepo;
        _rateLimiter = rateLimiter;
    }

    [Function("Coach")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "coach")] HttpRequest req)
    {
        _logger.LogInformation("Coach function triggered");

        // ── Bot detection ────────────────────────────────────────────
        var userAgent = req.Headers.UserAgent.ToString();
        if (RateLimitService.IsBotUserAgent(userAgent))
        {
            _logger.LogWarning("Blocked bot user-agent: {UserAgent}", userAgent);
            return new ObjectResult(new { error = "Forbidden" }) { StatusCode = 403 };
        }

        // ── Auth ─────────────────────────────────────────────────────
        var userId = AuthHelper.GetUserId(req);
        var isAuth = AuthHelper.IsAuthenticated(req);

        // ── Rate limiting ────────────────────────────────────────────
        var rateLimitKey = isAuth ? $"user:{userId}" : $"ip:{AuthHelper.GetClientIp(req)}";
        var rateCheck = _rateLimiter.Check(rateLimitKey, isAuth);

        if (rateCheck.IsLimited)
        {
            _logger.LogWarning("Rate limited: key={Key}, count={Count}/{Limit}",
                rateLimitKey, rateCheck.CurrentCount, rateCheck.Limit);

            var result = new ObjectResult(new
            {
                error = "rate_limited",
                message = "You've reached the request limit.",
                retryAfterSeconds = rateCheck.RetryAfterSeconds,
                isAnonymous = !isAuth
            })
            { StatusCode = 429 };

            req.HttpContext.Response.Headers["Retry-After"] = rateCheck.RetryAfterSeconds.ToString();
            return result;
        }

        // ── Validation & processing ──────────────────────────────────
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<CoachRequest>(requestBody, JsonOptions);

            if (request is null || string.IsNullOrWhiteSpace(request.Text))
            {
                return new BadRequestObjectResult(new { error = "Text is required" });
            }

            if (request.Text.Length < 10)
            {
                return new BadRequestObjectResult(new { error = "Text must be at least 10 characters" });
            }

            if (request.Text.Length > 5000)
            {
                return new BadRequestObjectResult(new { error = "Text must be 5000 characters or less" });
            }

            if (!Enum.IsDefined(typeof(AIModel), request.Model))
            {
                return new BadRequestObjectResult(new { error = "Invalid AI model selected" });
            }

            // ── Model access enforcement ──────────────────────────────
            if (!AuthHelper.IsAdmin(req))
            {
                var userTier = isAuth ? ModelAccessTier.Authenticated : ModelAccessTier.Free;
                var modelDetails = AIModelInfo.Get(request.Model);

                if (modelDetails.RequiredTier > userTier)
                {
                    var hint = modelDetails.RequiredTier == ModelAccessTier.Authenticated
                        ? "Sign in to unlock this model."
                        : "This model requires a premium plan (coming soon).";

                    _logger.LogWarning(
                        "Model access denied: {Model} requires {Required}, user tier is {UserTier}",
                        request.Model, modelDetails.RequiredTier, userTier);

                    return new ObjectResult(new
                    {
                        error = "model_access_denied",
                        message = hint,
                        requiredTier = modelDetails.RequiredTier.ToString()
                    })
                    { StatusCode = 403 };
                }
            }

            var response = await _coachService.AnalyzeAsync(request);

            // Persist for authenticated users, or always in dev (so local testing works)
            if (isAuth || AuthHelper.IsDevelopment)
            {
                _ = PersistSessionAsync(userId, request, response);
            }

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

    private async Task PersistSessionAsync(string userId, CoachRequest request, CoachResponse response)
    {
        try
        {
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
