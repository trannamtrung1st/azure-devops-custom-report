using System.Text.Json;
using AdoReport.WorkerApp.Persistence;
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
public class AzureDevOpsService : IAzureDevOpsService, IDisposable
{
    private const string SystemIdentity = "System";
    private static readonly string[] _nonTrackingFields =
    [
        "System.Description",
        "System.History",
        "Microsoft.VSTS.Common.AcceptanceCriteria",
        "Custom.BendendTechnicalApproach",
        "Custom.FrontendTechnicalApproach"
    ];

    private readonly int _daysToTrack;
    private readonly DateTime _trackChangesSince;
    private readonly string _organization;
    private readonly string _project;
    private readonly string _personalAccessToken;
    private readonly ILogger<AzureDevOpsService> _logger;
    private readonly AppDbContext _dbContext;
    private readonly WorkItemTrackingHttpClient _witClient;
    private readonly VssConnection _connection;

    public AzureDevOpsService(
        IConfiguration configuration,
        ILogger<AzureDevOpsService> logger,
        AppDbContext dbContext)
    {
        _daysToTrack = configuration.GetValue<int>("AppSettings:DaysToTrack");
        _trackChangesSince = configuration.GetValue<DateTime>("AppSettings:TrackChangesSince");
        _organization = configuration["AzureDevOps:Organization"] ?? throw new ArgumentNullException("AzureDevOps:Organization");
        _project = configuration["AzureDevOps:Project"] ?? throw new ArgumentNullException("AzureDevOps:Project");
        _personalAccessToken = configuration["AzureDevOps:PersonalAccessToken"] ?? throw new ArgumentNullException("AzureDevOps:PersonalAccessToken");
        _logger = logger;
        _dbContext = dbContext;

        var credentials = new VssBasicCredential(string.Empty, _personalAccessToken);
        _connection = new VssConnection(new Uri($"https://dev.azure.com/{_organization}"), credentials);
        _witClient = _connection.GetClient<WorkItemTrackingHttpClient>();
    }

    public async Task<IEnumerable<WorkItem>> QueryTrackingEpicAndFeatures(CancellationToken cancellationToken = default)
    {
        var wiql = new Wiql()
        {
            Query = @$"
SELECT * FROM WorkItems
WHERE [System.WorkItemType] IN ('Feature', 'Epic')
AND [Area Path] UNDER 'Asset Backlogs'
AND [System.State] NOT IN ('Removed', 'Closed')
"
        };

        return await QueryWorkItems(wiql, cancellationToken);
    }

    public async Task<IEnumerable<WorkItem>> QueryTrackingUserStories(CancellationToken cancellationToken = default)
    {
        var wiql = new Wiql()
        {
            Query = @$"
SELECT * FROM WorkItems
WHERE [System.WorkItemType] = 'User Story'
AND [Area Path] UNDER 'Asset Backlogs'
AND [Area Path] NOT IN ('Asset Backlogs\Story Pool')
AND [System.State] EVER 'Active'
AND [System.State] NOT IN ('Removed', 'Closed')
AND [System.ChangedDate] >= @Today - {_daysToTrack}
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
AND [System.ChangedDate] >= @Today - {_daysToTrack}
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
AND [System.ChangedDate] >= @Today - {_daysToTrack}
"
        };

        return await QueryWorkItems(wiql, cancellationToken);
    }

    public async Task<IEnumerable<WorkItem>> QueryWorkItems(Wiql wiql, CancellationToken cancellationToken = default)
    {
        var result = await _witClient.QueryByWiqlAsync(wiql, project: _project, cancellationToken: cancellationToken);
        var workItems = new List<WorkItem>();

        foreach (var batch in result.WorkItems.Chunk(size: 200))
        {
            var currentBatch = await _witClient.GetWorkItemsAsync(
                project: _project,
                ids: batch.Select(wi => wi.Id),
                expand: WorkItemExpand.All,
                cancellationToken: cancellationToken);
            workItems.AddRange(currentBatch);

            LogWorkItems(currentBatch, "work items");
        }

        workItems.ForEach(wi =>
        {
            foreach (var nonTrackingField in _nonTrackingFields)
                if (wi.Fields.ContainsKey(nonTrackingField))
                    wi.Fields.Remove(nonTrackingField);
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
        var existingWorkItems = await _dbContext.WorkItems
            .AsNoTracking().Where(w => workItems.Select(wi => wi.Id).Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, cancellationToken);
        var affectedCount = 0;

        foreach (var workItem in workItems)
        {
            var existingWorkItem = existingWorkItems!.GetValueOrDefault(workItem.Id!.Value);

            var changedBy = workItem.Fields["System.ChangedBy"] is IdentityRef changedByIdentity
                ? changedByIdentity.UniqueName
                : SystemIdentity;

            if (existingWorkItem == null)
            {
                var newWorkItem = new WorkItemEntity
                {
                    Id = workItem.Id!.Value,
                    Rev = workItem.Rev,
                    Type = workItem.Fields["System.WorkItemType"]?.ToString() ?? string.Empty,
                    State = workItem.Fields["System.State"]?.ToString() ?? string.Empty,
                    Title = workItem.Fields["System.Title"]?.ToString() ?? string.Empty,
                    AreaPath = workItem.Fields.TryGetValue("System.AreaPath", out var areaPath) ? areaPath?.ToString() : null,
                    ParentId = workItem.Fields.TryGetValue("System.Parent", out var parentId) ? Convert.ToInt32(parentId) : null,
                    CreatedDate = Convert.ToDateTime(workItem.Fields["System.CreatedDate"]),
                    ChangedDate = workItem.Fields.TryGetValue("System.ChangedDate", out var changedDate) ? Convert.ToDateTime(changedDate) : null,
                    Fields = JsonSerializer.Serialize(workItem.Fields)
                };
                _dbContext.WorkItems.Add(newWorkItem);
                affectedCount++;

                await SyncRevisions(newWorkItem.Id, skip: 0, take: workItem.Rev ?? 1, cancellationToken);
            }
            else
            {
                // Track changes before updating
                var hasChanges = await TrySyncNewRevisions(existingWorkItem, workItem, cancellationToken);

                if (hasChanges)
                {
                    // Update the work item
                    existingWorkItem.Rev = workItem.Rev;
                    existingWorkItem.Type = workItem.Fields["System.WorkItemType"]?.ToString() ?? string.Empty;
                    existingWorkItem.State = workItem.Fields["System.State"]?.ToString() ?? string.Empty;
                    existingWorkItem.Title = workItem.Fields["System.Title"]?.ToString() ?? string.Empty;
                    existingWorkItem.AreaPath = workItem.Fields.TryGetValue("System.AreaPath", out var areaPath) ? areaPath?.ToString() : null;
                    existingWorkItem.ParentId = workItem.Fields.TryGetValue("System.Parent", out var parentId) ? Convert.ToInt32(parentId) : null;
                    existingWorkItem.CreatedDate = Convert.ToDateTime(workItem.Fields["System.CreatedDate"]);
                    existingWorkItem.ChangedDate = workItem.Fields.TryGetValue("System.ChangedDate", out var changedDate) ? Convert.ToDateTime(changedDate) : null;
                    existingWorkItem.Fields = JsonSerializer.Serialize(workItem.Fields);

                    _dbContext.Update(existingWorkItem);
                    affectedCount++;
                }
            }
        }

        if (affectedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Saved {count} work items to database", affectedCount);
        }
    }

    public async Task SaveAllWorkItemsToDatabase(CancellationToken cancellationToken = default)
    {
        var epicAndFeatures = await QueryTrackingEpicAndFeatures(cancellationToken);
        await SaveWorkItemsToDatabase(epicAndFeatures, cancellationToken);

        var userStories = await QueryTrackingUserStories(cancellationToken);
        userStories = userStories.Where(us => us.Id.HasValue);
        var activeUsIds = userStories.Select(us => us.Id!.Value).ToArray();
        await SaveWorkItemsToDatabase(userStories, cancellationToken);
        var closedUsIds = await _dbContext.WorkItems
            .AsNoTracking().Where(w => w.Type == "User Story" && !activeUsIds.Contains(w.Id))
            .Select(w => w.Id)
            .ToArrayAsync(cancellationToken);

        if (closedUsIds.Length > 0)
            await CloseWorkItems(closedUsIds, cancellationToken);

        foreach (var usBatch in userStories.Chunk(size: 20))
        {
            var tasks = await QueryTrackingTasks(usBatch.Select(us => us.Id!.Value), cancellationToken);
            var bugs = await QueryTrackingBugs(usBatch.Select(us => us.Id!.Value), cancellationToken);
            await SaveWorkItemsToDatabase(tasks, cancellationToken);
            await SaveWorkItemsToDatabase(bugs, cancellationToken);
        }
    }

    private async Task CloseWorkItems(IEnumerable<int> workItemIds, CancellationToken cancellationToken = default)
    {
        var workItems = await _dbContext.WorkItems.Where(w => workItemIds.Contains(w.Id)).ToListAsync(cancellationToken);
        workItems.ForEach(w => w.State = "Closed");
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Closed {count} work items", workItems.Count);
    }

    private async Task<bool> TrySyncNewRevisions(WorkItemEntity existingWorkItem, WorkItem remoteWorkItem, CancellationToken cancellationToken)
    {
        if (existingWorkItem.Rev >= remoteWorkItem.Rev)
            return false;

        var skip = existingWorkItem.Rev;
        var take = remoteWorkItem.Rev - existingWorkItem.Rev;
        await SyncRevisions(existingWorkItem.Id, skip ?? 0, take ?? 1, cancellationToken);
        return true;
    }

    private static bool IsNonTrackingField(string field) => _nonTrackingFields.Contains(field);

    private async Task SyncRevisions(int workItemId, int skip, int take, CancellationToken cancellationToken)
    {
        const int MaxTake = 100;
        _logger.LogDebug("Syncing revisions for work item {id}, skip: {skip}, take: {take}", workItemId, skip, take);
        var changes = new List<WorkItemChangeEntity>();

        async Task SyncRevisions(int skip, int take)
        {
            var updates = await _witClient.GetUpdatesAsync(
                project: _project,
                id: workItemId,
                skip: skip,
                top: take,
                cancellationToken: cancellationToken);

            foreach (var update in updates)
            {
                if (update.RevisedDate < _trackChangesSince)
                    continue;

                var before = new Dictionary<string, object>();
                var after = new Dictionary<string, object>();

                if (update.Fields?.Count > 0)
                {
                    foreach (var field in update.Fields)
                    {
                        if (IsNonTrackingField(field.Key))
                            continue;

                        before[field.Key] = field.Value.OldValue;
                        after[field.Key] = field.Value.NewValue;
                    }
                }

                var change = new WorkItemChangeEntity
                {
                    WorkItemId = workItemId,
                    Rev = update.Rev,
                    ChangedDate = update.RevisedDate,
                    ChangedBy = update.RevisedBy.UniqueName ?? SystemIdentity,
                    BeforeFields = JsonSerializer.Serialize(before),
                    AfterFields = JsonSerializer.Serialize(after),
                };
                changes.Add(change);
            }
        }

        while (take > MaxTake)
        {
            await SyncRevisions(skip, MaxTake);
            skip += MaxTake;
            take -= MaxTake;
        }

        if (take > 0)
            await SyncRevisions(skip, take);

        await _dbContext.WorkItemChanges.AddRangeAsync(changes, cancellationToken);
    }

    [Obsolete]
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    private async Task<bool> TrackChanges(WorkItemEntity existingWorkItem, IDictionary<string, object?> newFields, string changedBy)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
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

        var hasChanges = beforeFields.Count > 0 || afterFields.Count > 0;
        // Only create change record if there are actual changes
        if (hasChanges)
        {
            var change = new WorkItemChangeEntity
            {
                WorkItemId = existingWorkItem.Id,
                ChangedDate = DateTime.UtcNow,
                ChangedBy = changedBy ?? SystemIdentity,
                BeforeFields = JsonSerializer.Serialize(beforeFields),
                AfterFields = JsonSerializer.Serialize(afterFields)
            };

            _dbContext.WorkItemChanges.Add(change);
        }

        return hasChanges;
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

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _witClient.Dispose();
        _connection.Dispose();
    }
}
