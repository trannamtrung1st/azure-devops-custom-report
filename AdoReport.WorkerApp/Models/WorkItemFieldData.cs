using System.Text.Json.Serialization;

namespace AdoReport.WorkerApp.Models;

public class WorkItemFieldData
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Value { get; set; }
}
