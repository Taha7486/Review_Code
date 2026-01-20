using System.Collections.Concurrent;
using System.Threading.Channels;
using dotnet_api.Data;
using dotnet_api.Models;
using dotnet_api.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace dotnet_api.Services;

public class BackgroundAnalysisProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundAnalysisProcessor> _logger;
    private readonly Channel<AnalysisJob> _jobQueue;

    public BackgroundAnalysisProcessor(
        IServiceProvider serviceProvider,
        ILogger<BackgroundAnalysisProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        var options = new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _jobQueue = Channel.CreateBounded<AnalysisJob>(options);
    }

    public async Task<bool> QueueAnalysisAsync(AnalysisJob job)
    {
        try
        {
            await _jobQueue.Writer.WriteAsync(job);
            _logger.LogDebug("Queued analysis job for run {RunId}", job.RunId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue analysis job for run {RunId}", job.RunId);
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background analysis processor started");

        await foreach (var job in _jobQueue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAnalysisJobAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing analysis job for run {RunId}", job.RunId);
                
                // Mark run as failed
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var run = await context.AnalysisRuns
                    .AsTracking()
                    .FirstOrDefaultAsync(r => r.Id == job.RunId, stoppingToken);
                if (run != null)
                {
                    run.Status = "failed";
                    run.Summary = $"Analysis failed: {ex.Message}";
                    run.CompletedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync(stoppingToken);
                }
            }
        }
    }

    private async Task ProcessAnalysisJobAsync(AnalysisJob job, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var analysisService = scope.ServiceProvider.GetRequiredService<IAnalysisService>();
        
        _logger.LogDebug("Processing analysis job for run {RunId}", job.RunId);
        
        // Timeout after 10 minutes to prevent zombie processes
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cts.CancelAfter(TimeSpan.FromMinutes(10));
        
        try
        {
            // Process the analysis (this will update the run status)
            await analysisService.ProcessAnalysisAsync(job.RunId, job.Request, job.UserId, job.CorrelationId, cts.Token);
            
            _logger.LogInformation("Completed analysis job for run {RunId}", job.RunId);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
        {
            _logger.LogError("Analysis job for run {RunId} timed out after 10 minutes", job.RunId);
            // Mark as failed in DB handled by caller or we can do it here if we want specific timeout message
            throw new TimeoutException("Analysis timed out");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Analysis job for run {RunId} was cancelled by host shutdown", job.RunId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing analysis job for run {RunId}", job.RunId);
            throw;
        }
    }
}

public class AnalysisJob
{
    public int RunId { get; set; }
    public AnalyzeBranchDto Request { get; set; } = null!;
    public int UserId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}
