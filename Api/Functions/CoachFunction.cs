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
    private readonly ILogger<CoachFunction> _logger;
    private readonly ICoachService _coachService;

    public CoachFunction(ILogger<CoachFunction> logger, ICoachService coachService)
    {
        _logger = logger;
        _coachService = coachService;
    }

    [Function("Coach")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "coach")] HttpRequest req)
    {
        _logger.LogInformation("Coach function triggered");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<CoachRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request is null || string.IsNullOrWhiteSpace(request.Text))
            {
                return new BadRequestObjectResult(new { error = "Text is required" });
            }

            if (request.Text.Length > 5000)
            {
                return new BadRequestObjectResult(new { error = "Text must be 5000 characters or less" });
            }

            var response = await _coachService.AnalyzeAsync(request);
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
}

