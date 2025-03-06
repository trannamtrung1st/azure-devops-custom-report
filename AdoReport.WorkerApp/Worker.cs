using AdoReport.WorkerApp.Services.Abstractions;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AdoReport.WorkerApp;

/// <summary>
/// Background service that retrieves work items from Azure DevOps
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IAzureDevOpsService _azureDevOpsService;

    public Worker(ILogger<Worker> logger, IAzureDevOpsService azureDevOpsService)
    {
        _logger = logger;
        _azureDevOpsService = azureDevOpsService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Worker starting at: {time}", DateTimeOffset.Now);

            var workItems = await _azureDevOpsService.QueryAllTrackingWorkItems(stoppingToken);

            _logger.LogInformation("Worker completed at: {time}", DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while processing work items");
        }

        // Wait for 1 hour before running again
        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
    }
}
