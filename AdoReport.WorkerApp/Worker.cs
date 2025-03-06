using AdoReport.WorkerApp.Services.Abstractions;

namespace AdoReport.WorkerApp;

/// <summary>
/// Background service that retrieves work items from Azure DevOps
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Worker starting at: {time}", DateTimeOffset.Now);

                using (var scope = _serviceProvider.CreateScope())
                {
                    var azureDevOpsService = scope.ServiceProvider.GetRequiredService<IAzureDevOpsService>();
                    await azureDevOpsService.SaveAllWorkItemsToDatabase(stoppingToken);
                }

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
}
