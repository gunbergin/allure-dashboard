using Quartz;
using Microsoft.Extensions.Logging;
using Job;

namespace Job.Jobs;

/// <summary>
/// Quartz job wrapper for processing Allure JSON files
/// Executes IAllureJsonJob on a scheduled basis (e.g., every 5 minutes)
/// </summary>
public class AllureJsonProcessingJob : IJob
{
    private readonly IAllureJsonJob _allureJsonJob;
    private readonly ILogger<AllureJsonProcessingJob> _logger;

    public AllureJsonProcessingJob(IAllureJsonJob allureJsonJob, ILogger<AllureJsonProcessingJob> logger)
    {
        _allureJsonJob = allureJsonJob;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            _logger.LogInformation("🔄 Starting AllureJsonProcessingJob execution at {Time}", DateTime.Now);
            
            // Use default values for RunId and TestRunId since this is a scheduled job
            var runId = Guid.NewGuid().ToString();
            var testRunId = Guid.NewGuid().ToString();
            
            await _allureJsonJob.QueueAllureJsonProcessingAsync(runId, testRunId);
            
            _logger.LogInformation("✅ Completed AllureJsonProcessingJob execution at {Time}", DateTime.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during AllureJsonProcessingJob execution");
            throw;
        }
    }
}
