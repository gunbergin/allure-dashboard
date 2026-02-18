namespace Job;

/// <summary>
/// Background job for asynchronously processing Allure JSON files
/// </summary>
public interface IAllureJsonJob
{
    /// <summary>
    /// Queue an async job to process Allure JSON files
    /// Job runs in background (fire-and-forget pattern)
    /// </summary>
    /// <param name="runId">Test run ID</param>
    /// <param name="testRunId">Test run identifier for persistence</param>
    /// <returns>Task that completes when job is queued (not when job finishes)</returns>
    Task QueueAllureJsonProcessingAsync(string runId, string testRunId);
}
