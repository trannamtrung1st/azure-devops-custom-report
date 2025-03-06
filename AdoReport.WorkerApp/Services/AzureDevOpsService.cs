using System.Text.Json;
using AdoReport.WorkerApp.Data;
using AdoReport.WorkerApp.Models;
using AdoReport.WorkerApp.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
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
    private readonly AppDbContext _dbContext;

    public AzureDevOpsService(
        IConfiguration configuration,
        ILogger<AzureDevOpsService> logger,
        AppDbContext dbContext)
    {
        _organization = configuration["AzureDevOps:Organization"] ?? throw new ArgumentNullException("AzureDevOps:Organization");
        _project = configuration["AzureDevOps:Project"] ?? throw new ArgumentNullException("AzureDevOps:Project");
        _personalAccessToken = configuration["AzureDevOps:PersonalAccessToken"] ?? throw new ArgumentNullException("AzureDevOps:PersonalAccessToken");
        _logger = logger;
        _dbContext = dbContext;
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

        workItems.ForEach(wi =>
        {
            if (wi.Fields.ContainsKey("System.Description"))
                wi.Fields.Remove("System.Description");

            if (wi.Fields.ContainsKey("System.History"))
                wi.Fields.Remove("System.History");

            if (wi.Fields.ContainsKey("Microsoft.VSTS.Common.AcceptanceCriteria"))
                wi.Fields.Remove("Microsoft.VSTS.Common.AcceptanceCriteria");

            if (wi.Fields.ContainsKey("Custom.BendendTechnicalApproach"))
                wi.Fields.Remove("Custom.BendendTechnicalApproach");

            if (wi.Fields.ContainsKey("Custom.FrontendTechnicalApproach"))
                wi.Fields.Remove("Custom.FrontendTechnicalApproach");
        });

        return workItems;
    }

    public void LogWorkItems(IEnumerable<WorkItem> workItems, string description)
    {
        _logger.LogDebug("Retrieved {count} {description}", workItems.Count(), description);

        foreach (var workItem in workItems)
        {
            _logger.LogDebug("Work Item ID: {id}, Title: {title}, Type: {type}, State: {state}",
                workItem.Id,
                workItem.Fields.ContainsKey("System.Title") ? workItem.Fields["System.Title"] : "No Title",
                workItem.Fields.ContainsKey("System.WorkItemType") ? workItem.Fields["System.WorkItemType"] : "Unknown Type",
                workItem.Fields.ContainsKey("System.State") ? workItem.Fields["System.State"] : "Unknown State");
        }
    }

    public async Task SaveWorkItemsToDatabase(IEnumerable<WorkItem> workItems, CancellationToken cancellationToken = default)
    {
        foreach (var workItem in workItems)
        {
            var existingWorkItem = await _dbContext.WorkItems
                .FirstOrDefaultAsync(w => w.Id == workItem.Id, cancellationToken);

            var changedBy = workItem.Fields["System.ChangedBy"] is IdentityRef changedByIdentity
                ? changedByIdentity.UniqueName
                : "system";

            if (existingWorkItem == null)
            {
                existingWorkItem = new WorkItemEntity
                {
                    Id = workItem.Id!.Value,
                    Type = workItem.Fields["System.WorkItemType"]?.ToString() ?? string.Empty,
                    State = workItem.Fields["System.State"]?.ToString() ?? string.Empty,
                    Title = workItem.Fields["System.Title"]?.ToString() ?? string.Empty,
                    AreaPath = workItem.Fields.ContainsKey("System.AreaPath") ? workItem.Fields["System.AreaPath"]?.ToString() : null,
                    ParentId = workItem.Fields.ContainsKey("System.Parent") ? Convert.ToInt32(workItem.Fields["System.Parent"]) : null,
                    CreatedDate = Convert.ToDateTime(workItem.Fields["System.CreatedDate"]),
                    ChangedDate = workItem.Fields.ContainsKey("System.ChangedDate") ? Convert.ToDateTime(workItem.Fields["System.ChangedDate"]) : null,
                    Fields = JsonSerializer.Serialize(workItem.Fields)
                };
                _dbContext.WorkItems.Add(existingWorkItem);

                // Track initial state as a change (with null before state)
                var change = new WorkItemChangeEntity
                {
                    WorkItemId = workItem.Id!.Value,
                    ChangedDate = DateTime.UtcNow,
                    ChangedBy = changedBy,
                    BeforeFields = null,
                    AfterFields = JsonSerializer.Serialize(workItem.Fields)
                };
                _dbContext.WorkItemChanges.Add(change);
            }
            else
            {
                // Track changes before updating
                await TrackChanges(existingWorkItem, workItem.Fields, changedBy);

                // Update the work item
                existingWorkItem.Type = workItem.Fields["System.WorkItemType"]?.ToString() ?? string.Empty;
                existingWorkItem.State = workItem.Fields["System.State"]?.ToString() ?? string.Empty;
                existingWorkItem.Title = workItem.Fields["System.Title"]?.ToString() ?? string.Empty;
                existingWorkItem.AreaPath = workItem.Fields.ContainsKey("System.AreaPath") ? workItem.Fields["System.AreaPath"]?.ToString() : null;
                existingWorkItem.ParentId = workItem.Fields.ContainsKey("System.Parent") ? Convert.ToInt32(workItem.Fields["System.Parent"]) : null;
                existingWorkItem.CreatedDate = Convert.ToDateTime(workItem.Fields["System.CreatedDate"]);
                existingWorkItem.ChangedDate = workItem.Fields.ContainsKey("System.ChangedDate") ? Convert.ToDateTime(workItem.Fields["System.ChangedDate"]) : null;
                existingWorkItem.Fields = JsonSerializer.Serialize(workItem.Fields);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Saved {count} work items to database", workItems.Count());
    }

    public async Task SaveAllWorkItemsToDatabase(CancellationToken cancellationToken = default)
    {
        var userStories = await QueryTrackingUserStories(cancellationToken);
        userStories = userStories.Where(us => us.Id.HasValue);
        await SaveWorkItemsToDatabase(userStories, cancellationToken);

        foreach (var usBatch in userStories.Chunk(size: 20))
        {
            var tasks = await QueryTrackingTasks(usBatch.Select(us => us.Id!.Value), cancellationToken);
            var bugs = await QueryTrackingBugs(usBatch.Select(us => us.Id!.Value), cancellationToken);
            await SaveWorkItemsToDatabase(tasks, cancellationToken);
            await SaveWorkItemsToDatabase(bugs, cancellationToken);
        }
    }


    private async Task TrackChanges(WorkItemEntity existingWorkItem, IDictionary<string, object?> newFields, string changedBy)
    {
        var beforeFields = new Dictionary<string, object?>();
        var afterFields = new Dictionary<string, object?>();
        var fields = JsonSerializer.Deserialize<Dictionary<string, object>>(existingWorkItem.Fields ?? "{}");

        // Compare and track changes
        foreach (var field in newFields)
        {
            if (fields!.TryGetValue(field.Key, out var existingValue))
            {
                var newValueStr = JsonSerializer.Serialize(field.Value);
                var existingValueStr = JsonSerializer.Serialize(existingValue);
                var newValueJson = JsonDocument.Parse(newValueStr).RootElement;
                var existingValueJson = JsonDocument.Parse(existingValueStr).RootElement;

                if (!IsJsonSemanticallyEqual(newValueJson, existingValueJson))
                {
                    beforeFields[field.Key] = existingValue;
                    afterFields[field.Key] = field.Value;
                }
            }
            else if (field.Value != null)
            {
                // New field added
                afterFields[field.Key] = field.Value;
            }
        }

        // Check for removed fields
        foreach (var existingField in fields!)
        {
            if (!newFields.ContainsKey(existingField.Key))
            {
                beforeFields[existingField.Key] = existingField.Value;
            }
        }

        // Only create change record if there are actual changes
        if (beforeFields.Count > 0 || afterFields.Count > 0)
        {
            var change = new WorkItemChangeEntity
            {
                WorkItemId = existingWorkItem.Id,
                ChangedDate = DateTime.UtcNow,
                ChangedBy = changedBy,
                BeforeFields = JsonSerializer.Serialize(beforeFields),
                AfterFields = JsonSerializer.Serialize(afterFields)
            };

            _dbContext.WorkItemChanges.Add(change);
            await _dbContext.SaveChangesAsync();
        }
    }

    private static bool IsJsonSemanticallyEqual(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            // Handle type coercion for numbers and strings
            if ((a.ValueKind == JsonValueKind.String && b.ValueKind == JsonValueKind.Number) ||
                (a.ValueKind == JsonValueKind.Number && b.ValueKind == JsonValueKind.String))
            {
                var aStr = a.ValueKind == JsonValueKind.String ? a.GetString() : a.GetRawText();
                var bStr = b.ValueKind == JsonValueKind.String ? b.GetString() : b.GetRawText();
                return aStr == bStr;
            }
            return false;
        }

        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                var aProps = a.EnumerateObject().ToDictionary(p => p.Name);
                var bProps = b.EnumerateObject().ToDictionary(p => p.Name);

                // Check if they have the same properties
                if (!aProps.Keys.OrderBy(k => k).SequenceEqual(bProps.Keys.OrderBy(k => k)))
                    return false;

                // Compare each property value recursively
                return aProps.All(kvp => IsJsonSemanticallyEqual(kvp.Value.Value, bProps[kvp.Key].Value));

            case JsonValueKind.Array:
                var aArr = a.EnumerateArray().ToList();
                var bArr = b.EnumerateArray().ToList();

                if (aArr.Count != bArr.Count)
                    return false;

                // Sort arrays if they contain primitive values
                if (aArr.All(e => e.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
                {
                    var aSorted = aArr.OrderBy(e => e.ToString());
                    var bSorted = bArr.OrderBy(e => e.ToString());
                    return aSorted.Zip(bSorted, IsJsonSemanticallyEqual).All(x => x);
                }

                // For arrays of objects, compare elements in order
                return aArr.Zip(bArr, IsJsonSemanticallyEqual).All(x => x);

            case JsonValueKind.String:
                return string.Equals(a.GetString(), b.GetString(), StringComparison.Ordinal);

            case JsonValueKind.Number:
                // Compare numbers as strings to handle precision consistently
                return a.GetRawText() == b.GetRawText();

            case JsonValueKind.True:
            case JsonValueKind.False:
                return a.GetBoolean() == b.GetBoolean();

            case JsonValueKind.Null:
                return true;

            default:
                return false;
        }
    }
}
