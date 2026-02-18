using Job.Models;
using Newtonsoft.Json;

namespace Job.Data;

/// <summary>
/// Service to read Allure JSON files and persist them to Oracle
/// </summary>
public interface IAllureJsonService
{  
    Task PersistAllureResultAsync(AllureTestResult allureResult, IAllureRepository repository, IAllureStepRepository stepRepository, string? runId = null);
    Task<AllureTestResult?> ReadAllureJsonFileAsync(string filePath);
}

public class AllureJsonService : IAllureJsonService
{
   

    public async Task<AllureTestResult?> ReadAllureJsonFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Allure JSON file not found: {filePath}");
                return null;
            }

            var content = await File.ReadAllTextAsync(filePath);
            Console.WriteLine($"[AllureJsonService] Raw JSON content length: {content.Length} chars");
            
            var result = JsonConvert.DeserializeObject<AllureTestResult>(content);
            
            if (result != null)
            {
                Console.WriteLine($"[AllureJsonService] Deserialized: Name={result.Name}, Status={result.Status}, Steps={result.Steps?.Count}, Attachments={result.Attachments?.Count}");
                
                if (result.Attachments != null)
                {
                    Console.WriteLine($"[AllureJsonService] ROOT Attachments ({result.Attachments.Count}):");
                    foreach (var att in result.Attachments)
                    {
                        Console.WriteLine($"[AllureJsonService]   - Name: {att.Name}, Type: {att.Type}, Source: {att.Source}");
                    }
                }
                
                if (result.Steps != null)
                {
                    foreach (var step in result.Steps)
                    {
                        if (step.Attachments != null && step.Attachments.Count > 0)
                        {
                            Console.WriteLine($"[AllureJsonService] STEP '{step.Name}' Attachments ({step.Attachments.Count}):");
                            foreach (var att in step.Attachments)
                            {
                                Console.WriteLine($"[AllureJsonService]   - Name: {att.Name}, Type: {att.Type}, Source: {att.Source}");
                            }
                        }
                    }
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading Allure JSON file: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    public async Task PersistAllureResultAsync(AllureTestResult allureResult, IAllureRepository repository, IAllureStepRepository stepRepository, string? runId = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(allureResult.Uuid))
            {
                Console.WriteLine("Allure result missing UUID, skipping persistence");
                return;
            }

            var exists = await repository.AllureResultExistsAsync(allureResult.Uuid);
            if (exists)
            {
                Console.WriteLine($"Allure result already persisted: {allureResult.Uuid}");
                return;
            }

            var dbModel = ConvertToDbModel(allureResult);
            if (!string.IsNullOrEmpty(runId))
            {
                dbModel.RunId = runId;
            }
            if (dbModel != null)
            {
               var resultID= await repository.InsertAllureResultAsync(dbModel);
                Console.WriteLine($"[AllureJsonService] Persisted Allure result: {allureResult.Name} (ID: {resultID})");

                if (allureResult.Steps != null && allureResult.Steps.Count > 0)
                {
                    Console.WriteLine($"[AllureJsonService] Extracting {allureResult.Steps.Count} steps from Allure result (ResultID: {resultID})");
                    Console.WriteLine($"[AllureJsonService] Root-level attachments: {allureResult.Attachments?.Count ?? 0}");
                    
                    var steps = ExtractSteps(allureResult.Steps, resultID, allureResult.Attachments);
                    Console.WriteLine($"[AllureJsonService] ExtractSteps returned {steps.Count} items");
                    
                    foreach (var step in steps)
                    {
                        Console.WriteLine($"[AllureJsonService] Step detail - Name: {step.Name}, Status: {step.Status}, ScreenshotBlob: {(step.ScreenshotBlob == null ? "NULL" : step.ScreenshotBlob.Length + " bytes")}");
                    }
                    
                    if (steps.Any())
                    {
                        Console.WriteLine($"[AllureJsonService] Extracted {steps.Count} steps total");
                        var stepsWithScreenshots = steps.Where(s => s.ScreenshotBlob != null).ToList();
                        Console.WriteLine($"[AllureJsonService] Steps with screenshots: {stepsWithScreenshots.Count}");
                        
                        await stepRepository.InsertAllureStepsAsync(steps);
                        Console.WriteLine($"[AllureJsonService] ✓ Persisted {steps.Count} steps for result: {allureResult.Name}");
                    }
                    else
                    {
                        Console.WriteLine($"[AllureJsonService] ⚠ No steps extracted");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error persisting Allure result: {ex.Message}");
        }
    }

    private List<AllureStepDb> ExtractSteps(List<AllureStep> steps, int resultId, List<AllureAttachment>? rootAttachments = null, int level = 0)
    {
        var result = new List<AllureStepDb>();

        foreach (var step in steps)
        {
            var duration = step.Stop - step.Start;
            
            Console.WriteLine($"[AllureJsonService] Processing step: {step.Name} - Status: {step.Status}");
            
            var screenshotBytes = ExtractScreenshotDataFromAttachments(step.Status, rootAttachments);
            
            if (screenshotBytes != null)
            {
                Console.WriteLine($"[AllureJsonService] ✓ Screenshot extracted: {screenshotBytes.Length} bytes");
            }
            else if (string.Equals(step.Status, "failed", StringComparison.OrdinalIgnoreCase) || 
                     string.Equals(step.Status, "broken", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[AllureJsonService] ⚠ Step is {step.Status} but no screenshot found");
            }
            
            var stepDb = new AllureStepDb
            {
                AllureResultId = resultId,
                Name = step.Name ?? "Unknown",
                Status = step.Status ?? "unknown",
                StartTime = step.Start,
                EndTime = step.Stop,
                DurationMs = duration,
                Stage = step.Stage,
                Message = step.StatusDetails?.Message?.Substring(0, Math.Min(step.StatusDetails.Message.Length, 1000)),
                Trace = step.StatusDetails?.Trace?.Substring(0, Math.Min(step.StatusDetails.Trace.Length, 2000)),
                AttachmentsCount = step.Attachments?.Count ?? 0,
                NestedStepsCount = step.Steps?.Count ?? 0,
                ScreenshotBlob = screenshotBytes,
                CreatedAt = DateTime.UtcNow
            };

            result.Add(stepDb);

            if (step.Steps != null && step.Steps.Count > 0 && level < 5)
            {
                result.AddRange(ExtractSteps(step.Steps, resultId, rootAttachments, level + 1));
            }
        }

        return result;
    }

    private byte[]? ExtractScreenshotDataFromAttachments(
     string? stepStatus,
     List<AllureAttachment>? rootAttachments = null)
        {
        Console.WriteLine(
            $"[AllureJsonService] ExtractScreenshot - Root attachments: {rootAttachments?.Count ?? 0}, Status: {stepStatus}");

        if (!string.Equals(stepStatus, "failed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(stepStatus, "broken", StringComparison.OrdinalIgnoreCase))
            {
            Console.WriteLine($"[AllureJsonService] Step status '{stepStatus}' is not failed/broken, skipping screenshot");
            return null;
            }

        if (rootAttachments == null || rootAttachments.Count == 0)
            {
            Console.WriteLine("[AllureJsonService] No ROOT attachments available for failed/broken step");
            return null;
            }

        Console.WriteLine($"[AllureJsonService] Checking ROOT attachments ({rootAttachments.Count})");
        var screenshotBytes = TryExtractScreenshotFromAttachmentList(rootAttachments);
        if (screenshotBytes != null)
            {
            Console.WriteLine($"[AllureJsonService] ✓ Found screenshot in ROOT attachments: {screenshotBytes.Length} bytes");
            return screenshotBytes;
            }

        Console.WriteLine("[AllureJsonService] ⚠ No screenshot found in ROOT attachments for failed/broken step");
        return null;
        }
        
    private byte[]? TryExtractScreenshotFromAttachmentList(List<AllureAttachment> attachments)
    {
        foreach (var att in attachments)
        {
            Console.WriteLine($"[AllureJsonService] Attachment:");
            Console.WriteLine($"[AllureJsonService]   - Name: {att.Name}");
            Console.WriteLine($"[AllureJsonService]   - Type: {att.Type}");
            Console.WriteLine($"[AllureJsonService]   - Source: {att.Source}");
        }

        var screenshot = attachments.FirstOrDefault(a => 
            !string.IsNullOrWhiteSpace(a.Type) && 
            (a.Type.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
             a.Name?.EndsWith(".png", StringComparison.OrdinalIgnoreCase) == true ||
             a.Name?.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) == true ||
             a.Name?.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) == true ||
             a.Source?.EndsWith(".png", StringComparison.OrdinalIgnoreCase) == true ||
             a.Source?.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) == true ||
             a.Source?.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) == true));

        if (screenshot == null)
        {
            Console.WriteLine($"[AllureJsonService] No image attachment found");
            return null;
        }

        var fileToFind = !string.IsNullOrWhiteSpace(screenshot.Name) ? screenshot.Name : screenshot.Source;
        
        if (string.IsNullOrWhiteSpace(fileToFind))
        {
            Console.WriteLine($"[AllureJsonService] Attachment has no usable Name or Source");
            return null;
        }

        Console.WriteLine($"[AllureJsonService] Found image attachment - Name: {screenshot.Name}, Source: {screenshot.Source}, Type: {screenshot.Type}");
        Console.WriteLine($"[AllureJsonService] Will look for file: {fileToFind}");

        byte[]? screenshotBytes = null;
        try
        {
            var allureResultsPath = Path.Combine(Directory.GetCurrentDirectory(), "allure-results");
            Console.WriteLine($"[AllureJsonService] Looking in: {allureResultsPath}");
            
            var screenshotFilePath = Path.Combine(allureResultsPath, fileToFind);
            Console.WriteLine($"[AllureJsonService] Trying: {screenshotFilePath}");

            if (File.Exists(screenshotFilePath))
            {
                screenshotBytes = File.ReadAllBytes(screenshotFilePath);
                Console.WriteLine($"[AllureJsonService] ✓ Read: {fileToFind} ({screenshotBytes.Length} bytes)");
                return screenshotBytes;
            }
            else
            {
                Console.WriteLine($"[AllureJsonService] ✗ Not found at: {screenshotFilePath}");
            }
            
            var fileName = Path.GetFileName(fileToFind);
            if (fileName != fileToFind)
            {
                screenshotFilePath = Path.Combine(allureResultsPath, fileName);
                Console.WriteLine($"[AllureJsonService] Trying filename only: {screenshotFilePath}");
                
                if (File.Exists(screenshotFilePath))
                {
                    screenshotBytes = File.ReadAllBytes(screenshotFilePath);
                    Console.WriteLine($"[AllureJsonService] ✓ Read: {fileName} ({screenshotBytes.Length} bytes)");
                    return screenshotBytes;
                }
                else
                {
                    Console.WriteLine($"[AllureJsonService] ✗ Not found");
                }
            }
            
            if (!fileToFind.Contains("."))
            {
                Console.WriteLine($"[AllureJsonService] Source is UUID format, searching for screenshot_*.png");
                var screenshotFiles = Directory.GetFiles(allureResultsPath, "screenshot_*.png");
                
                if (screenshotFiles.Length > 0)
                {
                    Console.WriteLine($"[AllureJsonService] Found {screenshotFiles.Length} screenshot files");
                    var mostRecent = screenshotFiles
                        .OrderByDescending(f => File.GetLastWriteTime(f))
                        .FirstOrDefault();
                    
                    if (mostRecent != null)
                    {
                        screenshotBytes = File.ReadAllBytes(mostRecent);
                        Console.WriteLine($"[AllureJsonService] ✓ Read most recent: {Path.GetFileName(mostRecent)} ({screenshotBytes.Length} bytes)");
                        return screenshotBytes;
                    }
                }
                else
                {
                    Console.WriteLine($"[AllureJsonService] No screenshot_*.png files found");
                }
            }
            
            if (Directory.Exists(allureResultsPath))
            {
                var files = Directory.GetFiles(allureResultsPath, "*");
                Console.WriteLine($"[AllureJsonService] All files in allure-results: {string.Join(", ", files.Select(Path.GetFileName))}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AllureJsonService] ERROR: {ex.Message}");
            Console.WriteLine($"[AllureJsonService] Stack: {ex.StackTrace}");
        }

        if (screenshotBytes == null)
        {
            Console.WriteLine($"[AllureJsonService] ⚠ Screenshot bytes is NULL");
        }

        return screenshotBytes;
    }

    private AllureTestResultDb ConvertToDbModel(AllureTestResult allureResult)
    {
        var feature = allureResult.TitlePath?.Count > 0 
            ? string.Join("/", allureResult.TitlePath) 
            : "Unknown";

        var errorMessage = allureResult.StatusDetails?.Message ?? string.Empty;
        var stackTrace = allureResult.StatusDetails?.Trace ?? string.Empty;

        var labels = allureResult.Labels != null
        ? string.Join("; ", allureResult.Labels
            .Where(l => l.Name == "tag" && !string.IsNullOrWhiteSpace(l.Value))
            .Select(l => l.Value))
        : string.Empty;

        var duration = allureResult.Stop - allureResult.Start;

        return new AllureTestResultDb
        {
            Uuid = allureResult.Uuid ?? Guid.NewGuid().ToString(),
            HistoryId = allureResult.HistoryId,
            TestCaseId = allureResult.TestCaseId,
            FullName = allureResult.FullName,
            Name = allureResult.Name ?? "Unknown",
            Status = allureResult.Status ?? "unknown",
            DurationMs = duration,
            Feature = feature,
            ErrorMessage = errorMessage.Substring(0, Math.Min(errorMessage.Length, 2000)),
            StackTrace = stackTrace.Substring(0, Math.Min(stackTrace.Length, 4000)),
            Labels = labels,
            StepsCount = allureResult.Steps?.Count ?? 0,
            AttachmentsCount = allureResult.Attachments?.Count ?? 0,
            Known = allureResult.StatusDetails?.Known ?? false,
            Muted = allureResult.StatusDetails?.Muted ?? false,
            Flaky = allureResult.StatusDetails?.Flaky ?? false,
            StartTime = allureResult.Start,
            EndTime = allureResult.Stop,
            CreatedAt = DateTime.UtcNow
        };
    }
}
