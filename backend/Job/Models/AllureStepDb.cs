namespace Job.Models;

/// <summary>
/// Database model for storing individual Allure test steps with screenshots as binary data
/// </summary>
public class AllureStepDb
{
    public int Id { get; set; }
    public int AllureResultId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long StartTime { get; set; }
    public long EndTime { get; set; }
    public long DurationMs { get; set; }
    public string? Stage { get; set; }
    public string? Message { get; set; }
    public string? Trace { get; set; }
    public int AttachmentsCount { get; set; }
    public int NestedStepsCount { get; set; }
    public byte[]? ScreenshotBlob { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
