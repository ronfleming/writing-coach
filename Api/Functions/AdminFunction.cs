using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Api.Services;
using Api.Helpers;

namespace Api.Functions;

/// <summary>
/// Admin and user-data-management endpoints.
/// Admin endpoints require the "admin" role (gated via staticwebapp.config.json).
/// The /api/me/data endpoint lets authenticated users delete their own data (GDPR).
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
    /// DELETE /api/admin/clear?userId=xxx — admin-only: delete all data for a specific user.
    /// In production, gated to the "admin" role via staticwebapp.config.json.
    /// </summary>
    [Function("AdminClearData")]
    public async Task<IActionResult> ClearData(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "admin/clear")] HttpRequest req)
    {
        // In dev, allow without auth. In prod, SWA route rules enforce the admin role.
        var targetUserId = req.Query["userId"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            // Default to clearing the caller's own data
            targetUserId = AuthHelper.GetUserId(req);
        }

        _logger.LogWarning("⚠️ Admin clear requested for user {UserId}", targetUserId);

        try
        {
            var sessionsDeleted = await _sessionRepo.DeleteAllByUserAsync(targetUserId);
            var phrasesDeleted = await _phraseRepo.DeleteAllByUserAsync(targetUserId);

            _logger.LogWarning("Cleared data for user {UserId}: {Sessions} sessions, {Phrases} phrases",
                targetUserId, sessionsDeleted, phrasesDeleted);

            return new OkObjectResult(new
            {
                message = $"Cleared all data for user '{targetUserId}'",
                sessionsDeleted,
                phrasesDeleted
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing data for user {UserId}", targetUserId);
            return new ObjectResult(new { error = "Failed to clear data", detail = ex.Message })
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// DELETE /api/me/data — authenticated users can delete their own data (GDPR right to erasure).
    /// </summary>
    [Function("DeleteMyData")]
    public async Task<IActionResult> DeleteMyData(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "me/data")] HttpRequest req)
    {
        var userId = AuthHelper.GetUserId(req);

        if (userId == "anonymous")
        {
            return new ObjectResult(new { error = "Authentication required to delete data" })
            {
                StatusCode = 401
            };
        }

        _logger.LogWarning("User {UserId} requested deletion of all their data", userId);

        try
        {
            var sessionsDeleted = await _sessionRepo.DeleteAllByUserAsync(userId);
            var phrasesDeleted = await _phraseRepo.DeleteAllByUserAsync(userId);

            _logger.LogInformation("Deleted data for user {UserId}: {Sessions} sessions, {Phrases} phrases",
                userId, sessionsDeleted, phrasesDeleted);

            return new OkObjectResult(new
            {
                message = "All your data has been deleted",
                sessionsDeleted,
                phrasesDeleted
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting data for user {UserId}", userId);
            return new ObjectResult(new { error = "Failed to delete data" })
            {
                StatusCode = 500
            };
        }
    }
}
