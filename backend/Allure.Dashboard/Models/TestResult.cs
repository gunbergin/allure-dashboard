namespace Allure.Dashboard.Models;

using Newtonsoft.Json;

public class TestResult
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Status { get; set; } // passed, failed, skipped, broken
    public string? Project { get; set; }
    public List<string>? Tags { get; set; }
    public DateTime Timestamp { get; set; }
    public long Duration { get; set; }
    public string? HistoryId { get; set; }
    public string? Source { get; set; }
    public List<Step>? Steps { get; set; }
    public List<Attachment>? Attachments { get; set; }
}

public class Attachment
{
    [JsonProperty("name")]
    public string? Name { get; set; }
    
    [JsonProperty("source")]
    public string? Source { get; set; }
    
    [JsonProperty("type")]
    public string? Type { get; set; }
}

public class Label
{
    [JsonProperty("name")]
    public string? Name { get; set; }
    
    [JsonProperty("value")]
    public string? Value { get; set; }
}

public class StatusDetails
{
    [JsonProperty("known")]
    public bool Known { get; set; }
    
    [JsonProperty("muted")]
    public bool Muted { get; set; }
    
    [JsonProperty("flaky")]
    public bool Flaky { get; set; }
}

public class Step
{
    [JsonProperty("name")]
    public string? Name { get; set; }
    
    [JsonProperty("status")]
    public string? Status { get; set; }
    
    [JsonProperty("statusDetails")]
    public StatusDetails? StatusDetails { get; set; }
    
    [JsonProperty("stage")]
    public string? Stage { get; set; }
    
    [JsonProperty("start")]
    public long Start { get; set; }
    
    [JsonProperty("stop")]
    public long Stop { get; set; }
    
    [JsonProperty("steps")]
    public List<Step>? Steps { get; set; }
    
    [JsonProperty("attachments")]
    public List<Attachment>? Attachments { get; set; }
    
    [JsonProperty("parameters")]
    public List<object>? Parameters { get; set; }
}

public class TestReport
{
    [JsonProperty("uuid")]
    public string? Uuid { get; set; }
    
    [JsonProperty("historyId")]
    public string? HistoryId { get; set; }
    
    [JsonProperty("testCaseId")]
    public string? TestCaseId { get; set; }
    
    [JsonProperty("titlePath")]
    public List<string>? TitlePath { get; set; }
    
    [JsonProperty("fullName")]
    public string? FullName { get; set; }
    
    [JsonProperty("name")]
    public string? Name { get; set; }
    
    [JsonProperty("status")]
    public string? Status { get; set; }
    
    [JsonProperty("stage")]
    public string? Stage { get; set; }
    
    [JsonProperty("labels")]
    public List<Label>? Labels { get; set; }
    
    [JsonProperty("steps")]
    public List<Step>? Steps { get; set; }
    
    [JsonProperty("start")]
    public long Start { get; set; }
    
    [JsonProperty("stop")]
    public long Stop { get; set; }
    
    [JsonProperty("attachments")]
    public List<Attachment>? Attachments { get; set; }
    
    [JsonProperty("parameters")]
    public List<object>? Parameters { get; set; }
    
    [JsonProperty("links")]
    public List<object>? Links { get; set; }
}

public class FilterRequest
{
    public string? Project { get; set; }
    public List<string>? Tags { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Status { get; set; }
}

public class TestRun
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<TestResult>? Results { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public int BrokenCount { get; set; }
    public double PassRate { get; set; }
}

public class DashboardData
{
    public List<TestRun>? TestRuns { get; set; }
    public List<TestResult>? Results { get; set; }
    public Dictionary<string, int>? StatusCounts { get; set; }
    public List<string>? Projects { get; set; }
    public List<string>? AvailableTags { get; set; }
    public int TotalTests { get; set; }
    public double PassRate { get; set; }
}

public class TimeGroupedTestCase
{
    public string TimeGroup { get; set; } = string.Empty;
    public DateTime GroupTime { get; set; }
    public List<TestResult> TestCases { get; set; } = new();
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public int BrokenCount { get; set; }
    public double PassRate { get; set; }
}
