namespace AdoReport.WorkerApp.Models;

public class WorkItemEntity
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? AreaPath { get; set; }
    public int? ParentId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ChangedDate { get; set; }
    public string Fields { get; set; } = "{}";
}
