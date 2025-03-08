namespace AdoReport.WorkerApp.Models;

public class WorkItemChangeEntity
{
    public int Id { get; set; }
    public int WorkItemId { get; set; }
    public int? Rev { get; set; }
    public DateTime ChangedDate { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public string? BeforeFields { get; set; }
    public string? AfterFields { get; set; }
    public virtual WorkItemEntity WorkItem { get; set; } = null!;
}
