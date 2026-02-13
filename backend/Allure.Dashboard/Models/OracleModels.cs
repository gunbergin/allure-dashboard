namespace Allure.Dashboard.Models;

/// <summary>
/// Oracle Database Models for Nova Test Scenarios Execution Data
/// </summary>
/// 
/// <summary>
/// Represents a test scenario from TEST_SCENARIOS table
/// </summary>
public class OracleTestScenario
{
    public long Id { get; set; }
    public string? ScenarioName { get; set; }
    public string? FeatureName { get; set; }
    public string? Tags { get; set; }
    public string? Status { get; set; }
    public DateTime ExecutionStartTime { get; set; }
    public DateTime ExecutionEndTime { get; set; }
    public long DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public int StepsPassed { get; set; }
    public int StepsFailed { get; set; }
    public int StepsSkipped { get; set; }
    public string? BrowserName { get; set; }
    public string? Url { get; set; }
    public string? ScreenshotPath { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Represents an Allure result from ALLURE_RESULTS table
/// </summary>
public class OracleAllureResult
{
    public long Id { get; set; }
    public string? Uuid { get; set; }
    public string? HistoryId { get; set; }
    public string? TestCaseId { get; set; }
    public string? FullName { get; set; }
    public string? Name { get; set; }
    public string? Status { get; set; }
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
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Represents a test step from ALLURE_STEPS table
/// </summary>
public class OracleAllureStep
{
    public long Id { get; set; }
    public long AllureResultId { get; set; }
    public string? Name { get; set; }
    public string? Status { get; set; }
    public long StartTime { get; set; }
    public long EndTime { get; set; }
    public long DurationMs { get; set; }
    public string? Stage { get; set; }
    public string? Message { get; set; }
    public string? Trace { get; set; }
    public int AttachmentsCount { get; set; }
    public int NestedStepsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
