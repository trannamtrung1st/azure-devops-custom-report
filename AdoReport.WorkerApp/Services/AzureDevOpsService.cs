using AdoReport.WorkerApp.Services.Abstractions;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace AdoReport.WorkerApp.Services;

/// <summary>
/// Service for interacting with Azure DevOps
/// </summary>
public class AzureDevOpsService : IAzureDevOpsService
{
    private readonly string _organization;
    private readonly string _project;
    private readonly string _personalAccessToken;
    private readonly ILogger<AzureDevOpsService> _logger;

    public AzureDevOpsService(IConfiguration configuration, ILogger<AzureDevOpsService> logger)
    {
        _organization = configuration["AzureDevOps:Organization"] ?? throw new ArgumentNullException("AzureDevOps:Organization");
        _project = configuration["AzureDevOps:Project"] ?? throw new ArgumentNullException("AzureDevOps:Project");
        _personalAccessToken = configuration["AzureDevOps:PersonalAccessToken"] ?? throw new ArgumentNullException("AzureDevOps:PersonalAccessToken");
        _logger = logger;
    }

    public async Task<IEnumerable<WorkItem>> QueryAllTrackingWorkItems(CancellationToken cancellationToken = default)
    {
        var allWorkItems = new List<WorkItem>();
        var userStories = await QueryTrackingUserStories(cancellationToken);
        userStories = userStories.Where(us => us.Id.HasValue);
        allWorkItems.AddRange(userStories);

        foreach (var usBatch in userStories.Chunk(size: 20))
        {
            var tasks = await QueryTrackingTasks(usBatch.Select(us => us.Id!.Value), cancellationToken);
            var bugs = await QueryTrackingBugs(usBatch.Select(us => us.Id!.Value), cancellationToken);
            allWorkItems.AddRange(tasks);
            allWorkItems.AddRange(bugs);
        }

        return allWorkItems;
    }

    public async Task<IEnumerable<WorkItem>> QueryTrackingUserStories(CancellationToken cancellationToken = default)
    {
        var wiql = new Wiql()
        {
            Query = @"
SELECT * FROM WorkItems
WHERE [System.WorkItemType] = 'User Story'
AND [System.State] NOT IN ('Removed', 'Closed')
AND [System.State] EVER 'Active'
AND [Area Path] UNDER 'Asset Backlogs'
AND [Area Path] NOT IN ('Asset Backlogs\Story Pool')
"
        };

        return await QueryWorkItems(wiql, cancellationToken);
    }

    public async Task<IEnumerable<WorkItem>> QueryTrackingTasks(IEnumerable<int> userStoryIds, CancellationToken cancellationToken = default)
    {
        var wiql = new Wiql()
        {
            Query = $@"
SELECT * FROM WorkItems
WHERE [System.WorkItemType] = 'Task'
AND [System.State] NOT IN ('Removed')
AND [System.Parent] IN ({string.Join(",", userStoryIds)})
"
        };

        return await QueryWorkItems(wiql, cancellationToken);
    }

    public async Task<IEnumerable<WorkItem>> QueryTrackingBugs(IEnumerable<int> userStoryIds, CancellationToken cancellationToken = default)
    {
        var wiql = new Wiql()
        {
            Query = $@"
SELECT * FROM WorkItems
WHERE [System.WorkItemType] = 'Bug'
AND [System.State] NOT IN ('Removed')
AND [System.Parent] IN ({string.Join(",", userStoryIds)})
"
        };

        return await QueryWorkItems(wiql, cancellationToken);
    }

    public async Task<IEnumerable<WorkItem>> QueryWorkItems(Wiql wiql, CancellationToken cancellationToken = default)
    {
        var credentials = new VssBasicCredential(string.Empty, _personalAccessToken);
        var connection = new VssConnection(new Uri($"https://dev.azure.com/{_organization}"), credentials);
        var witClient = connection.GetClient<WorkItemTrackingHttpClient>();

        var result = await witClient.QueryByWiqlAsync(wiql, project: _project, cancellationToken: cancellationToken);
        var workItems = new List<WorkItem>();

        foreach (var batch in result.WorkItems.Chunk(size: 200))
        {
            var currentBatch = await witClient.GetWorkItemsAsync(
                project: _project,
                ids: batch.Select(wi => wi.Id),
                expand: WorkItemExpand.All,
                cancellationToken: cancellationToken);
            workItems.AddRange(currentBatch);

            LogWorkItems(currentBatch, "work items");
        }

        return workItems;
    }

    public void LogWorkItems(IEnumerable<WorkItem> workItems, string description)
    {
        _logger.LogInformation("Retrieved {count} {description}", workItems.Count(), description);

        foreach (var workItem in workItems)
        {
            _logger.LogInformation("Work Item ID: {id}, Title: {title}, Type: {type}, State: {state}",
                workItem.Id,
                workItem.Fields.ContainsKey("System.Title") ? workItem.Fields["System.Title"] : "No Title",
                workItem.Fields.ContainsKey("System.WorkItemType") ? workItem.Fields["System.WorkItemType"] : "Unknown Type",
                workItem.Fields.ContainsKey("System.State") ? workItem.Fields["System.State"] : "Unknown State");
        }
    }
}
