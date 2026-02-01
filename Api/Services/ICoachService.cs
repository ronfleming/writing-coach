using Shared.Models;

namespace Api.Services;

public interface ICoachService
{
    Task<CoachResponse> AnalyzeAsync(CoachRequest request);
}

