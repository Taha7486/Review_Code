using dotnet_api.Models.DTOs;
using dotnet_api.Models;

namespace dotnet_api.Services;

public interface IBackgroundAnalysisService
{
    Task<int> StartAnalysisAsync(AnalyzeBranchDto request, int userId, string correlationId);
    Task<AnalysisRun?> GetRunStatusAsync(int runId);
}
