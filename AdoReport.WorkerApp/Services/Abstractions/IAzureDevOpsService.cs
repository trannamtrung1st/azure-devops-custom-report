using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AdoReport.WorkerApp.Services.Abstractions;

/// <summary>
/// Interface for Azure DevOps operations
/// </summary>
public interface IAzureDevOpsService
{
    Task<IEnumerable<WorkItem>> QueryTrackingEpicAndFeatures(CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkItem>> QueryTrackingUserStories(CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkItem>> QueryTrackingTasks(IEnumerable<int> userStoryIds, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkItem>> QueryTrackingBugs(IEnumerable<int> userStoryIds, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkItem>> QueryWorkItems(Wiql wiql, CancellationToken cancellationToken = default);
    Task SaveWorkItemsToDatabase(IEnumerable<WorkItem> workItems, CancellationToken cancellationToken = default);
    Task SaveAllWorkItemsToDatabase(CancellationToken cancellationToken = default);
}
