using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AdoReport.WorkerApp.Services.Abstractions;

/// <summary>
/// Interface for Azure DevOps operations
/// </summary>
public interface IAzureDevOpsService
{
    Task<IEnumerable<WorkItem>> QueryTrackingUserStories(CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkItem>> QueryTrackingTasks(IEnumerable<int> userStoryIds, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkItem>> QueryTrackingBugs(IEnumerable<int> userStoryIds, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkItem>> QueryAllTrackingWorkItems(CancellationToken cancellationToken = default);
    void LogWorkItems(IEnumerable<WorkItem> workItems, string description);
}
