using Job;
using Job.Data;

namespace Job.Services;

/// <summary>
/// Background job implementation for asynchronously processing Allure JSON files
/// Runs in a fire-and-forget pattern on a thread pool thread
/// </summary>
public class AllureJsonJob : IAllureJsonJob
{
    private readonly IAllureJsonService _allureJsonService;
    private readonly IAllureRepository _allureRepository;
    private readonly IAllureStepRepository _allureStepRepository;

    public AllureJsonJob(
        IAllureJsonService allureJsonService,
        IAllureRepository allureRepository,
        IAllureStepRepository allureStepRepository)
    {
        _allureJsonService = allureJsonService;
        _allureRepository = allureRepository;
        _allureStepRepository = allureStepRepository;
    }

    /// <summary>
    /// Queue a background job to process Allure JSON files
    /// Returns immediately without waiting for processing to complete
    /// </summary>
    public async Task QueueAllureJsonProcessingAsync(string runId, string testRunId)
    {
        Console.WriteLine($"[AllureJsonJob] Queued: Processing Allure JSON for RunId={runId}");
        
        // Start processing in background - don't await
        _ = Task.Run(async () => 
        {
            try
            {
                await ProcessAllureJsonFilesAsync(runId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AllureJsonJob] ERROR: {ex.Message}");
                Console.WriteLine($"[AllureJsonJob] Stack: {ex.StackTrace}");
            }
        });
    }

    /// <summary>
    /// Process Allure JSON result files from the allure-results directory
    /// Waits for files to be created, deserializes them, and persists to database
    /// </summary>
    private async Task ProcessAllureJsonFilesAsync(string runId)
    {
        Console.WriteLine($"[AllureJsonJob] Starting background processing for RunId={runId}");
        
        try
        {
            var allureReportsPath = Path.Combine(Directory.GetCurrentDirectory(), "allure-results");
            Console.WriteLine($"[AllureJsonJob] Looking for allure-results at: {allureReportsPath}");
            
            if (!Directory.Exists(allureReportsPath))
            {
                Console.WriteLine($"[AllureJsonJob] ⚠ allure-results directory NOT FOUND");
                return;
            }

            Console.WriteLine($"[AllureJsonJob] ✓ allure-results directory EXISTS");

            // Wait for JSON files to be created by Allure plugin (retry up to 15 seconds)
            Console.WriteLine("[AllureJsonJob] ⏳ Waiting for Allure JSON files to be generated...");
            var jsonFiles = await WaitForAllureJsonFilesAsync(allureReportsPath, maxWaitMs: 15000);
            
            if (jsonFiles.Length == 0)
            {
                var allFiles = Directory.GetFiles(allureReportsPath);
                Console.WriteLine($"[AllureJsonJob] ⚠ NO JSON files found after wait. Files: {string.Join(", ", allFiles.Select(Path.GetFileName))}");
                return;
            }
            
            Console.WriteLine($"[AllureJsonJob] ✅ Found {jsonFiles.Length} JSON files:");
            
            foreach (var jsonFile in jsonFiles)
            {
                var fileInfo = new FileInfo(jsonFile);
                Console.WriteLine($"[AllureJsonJob]   ✓ {Path.GetFileName(jsonFile)} ({fileInfo.Length} bytes)");
            }
            
            // Process each JSON file
            foreach (var jsonFile in jsonFiles)
            {
                try
                {
                    Console.WriteLine($"[AllureJsonJob] Processing: {Path.GetFileName(jsonFile)}");
                    
                    var allureResult = await _allureJsonService.ReadAllureJsonFileAsync(jsonFile);
                    
                    if (allureResult == null)
                    {
                        Console.WriteLine($"[AllureJsonJob] ⚠ Failed to deserialize: {Path.GetFileName(jsonFile)}");
                        continue;
                    }
                    
                    if (string.IsNullOrWhiteSpace(allureResult.Uuid))
                    {
                        Console.WriteLine($"[AllureJsonJob] ⚠ Missing UUID");
                        continue;
                    }

                    // Check if already persisted
                    var exists = await _allureRepository.AllureResultExistsAsync(allureResult.Uuid);
                    if (!exists)
                    {
                        await _allureJsonService.PersistAllureResultAsync(allureResult, _allureRepository, _allureStepRepository, runId);
                        Console.WriteLine($"[AllureJsonJob] ✓ Persisted: {allureResult.Name}");
                    }
                    else
                    {
                        Console.WriteLine($"[AllureJsonJob] ℹ Already persisted: {allureResult.Uuid}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AllureJsonJob] ERROR processing {Path.GetFileName(jsonFile)}: {ex.Message}");
                }
            }
            
            Console.WriteLine($"[AllureJsonJob] ✅ Completed processing for RunId={runId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AllureJsonJob] ERROR: {ex.Message}");
            Console.WriteLine($"[AllureJsonJob] Stack: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Wait for Allure JSON result files to appear in the allure-results directory
    /// Retries for specified duration, checking every 100ms
    /// </summary>
    private async Task<string[]> WaitForAllureJsonFilesAsync(string allureReportsPath, int maxWaitMs = 15000)
    {
        
            
            var jsonFiles = Directory.GetFiles(allureReportsPath, "*.json", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileName(path)
                    .Contains("result", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            
            if (jsonFiles.Length > 0)
            {
                Console.WriteLine($"[AllureJsonJob] ✓ Found {jsonFiles.Length} JSON files");
                return jsonFiles;
            }
                 
        return Array.Empty<string>();
    }
}
