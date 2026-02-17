using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Api.Services;
using Api.Helpers;

namespace Api.Functions;

public class SessionsFunction
{
    private readonly ILogger<SessionsFunction> _logger;
    private readonly ISessionRepository _sessionRepo;

    public SessionsFunction(ILogger<SessionsFunction> logger, ISessionRepository sessionRepo)
    {
        _logger = logger;
        _sessionRepo = sessionRepo;
    }

    /// <summary>GET /api/sessions — list sessions for the authenticated user.</summary>
    [Function("GetSessions")]
    public async Task<IActionResult> GetSessions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sessions")] HttpRequest req)
    {
        var userId = AuthHelper.GetUserId(req);

        // In production, anonymous users have no persisted data → return empty.
        // In dev, allow "anonymous" so the full pipeline is testable locally.
        if (userId == "anonymous" && !AuthHelper.IsDevelopment)
            return new OkObjectResult(Array.Empty<object>());

        var limitStr = req.Query["limit"].FirstOrDefault();
        var limit = int.TryParse(limitStr, out var l) ? Math.Min(l, 50) : 20;

        _logger.LogInformation("Fetching sessions for user {UserId} (limit={Limit})", userId, limit);

        var sessions = await _sessionRepo.GetByUserAsync(userId, limit);
        return new OkObjectResult(sessions);
    }

    /// <summary>GET /api/sessions/{id} — get a single session.</summary>
    [Function("GetSession")]
    public async Task<IActionResult> GetSession(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sessions/{id}")] HttpRequest req,
        string id)
    {
        var userId = AuthHelper.GetUserId(req);

        if (userId == "anonymous" && !AuthHelper.IsDevelopment)
            return new NotFoundObjectResult(new { error = "Session not found" });

        _logger.LogInformation("Fetching session {SessionId} for user {UserId}", id, userId);

        var session = await _sessionRepo.GetByIdAsync(id, userId);

        if (session is null)
            return new NotFoundObjectResult(new { error = "Session not found" });

        return new OkObjectResult(session);
    }
}
