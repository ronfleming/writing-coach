using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Api.Services;

namespace Api.Functions;

/// <summary>
/// Dev/admin endpoints for data management. Gate by auth in production.
/// </summary>
public class AdminFunction
{
    private readonly ILogger<AdminFunction> _logger;
    private readonly ISessionRepository _sessionRepo;
    private readonly IPhraseRepository _phraseRepo;

    public AdminFunction(
        ILogger<AdminFunction> logger,
        ISessionRepository sessionRepo,
        IPhraseRepository phraseRepo)
    {
        _logger = logger;
        _sessionRepo = sessionRepo;
        _phraseRepo = phraseRepo;
    }

    /// <summary>
    /// DELETE /api/admin/clear — delete all sessions and phrases for a user.
    /// </summary>
    [Function("AdminClearData")]
    public async Task<IActionResult> ClearData(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "admin/clear")] HttpRequest req)
    {
        var userId = req.Query["userId"].FirstOrDefault() ?? "anonymous";

        _logger.LogWarning("⚠️ Admin clear requested for user {UserId}", userId);

        try
        {
            var sessionsDeleted = await _sessionRepo.DeleteAllByUserAsync(userId);
            var phrasesDeleted = await _phraseRepo.DeleteAllByUserAsync(userId);

            _logger.LogWarning("Cleared data for user {UserId}: {Sessions} sessions, {Phrases} phrases",
                userId, sessionsDeleted, phrasesDeleted);

            return new OkObjectResult(new
            {
                message = $"Cleared all data for user '{userId}'",
                sessionsDeleted,
                phrasesDeleted
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing data for user {UserId}", userId);
            return new ObjectResult(new { error = "Failed to clear data", detail = ex.Message })
            {
                StatusCode = 500
            };
        }
    }
}

