namespace Job.Models;

/// <summary>
/// Database model for storing Allure test results
/// </summary>
public class AllureTestResultDb
{
    public int Id { get; set; }
    public string? RunId { get; set; }
    public string Uuid { get; set; } = string.Empty;
    public string? HistoryId { get; set; }
    public string? TestCaseId { get; set; }
    public string? FullName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long? DurationMs { get; set; }
    public string? Feature { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public string? Labels { get; set; }
    public int StepsCount { get; set; }
    public int AttachmentsCount { get; set; }
    public bool Known { get; set; }
    public bool Muted { get; set; }
    public bool Flaky { get; set; }
    public long StartTime { get; set; }
    public long EndTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
