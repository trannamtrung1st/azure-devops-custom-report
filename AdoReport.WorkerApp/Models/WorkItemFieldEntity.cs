namespace AdoReport.WorkerApp.Models;

public class WorkItemFieldEntity
{
    public int Id { get; set; }
    public int WorkItemId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string FieldData { get; set; } = "{}";
    public virtual WorkItemEntity WorkItem { get; set; } = null!;
}
