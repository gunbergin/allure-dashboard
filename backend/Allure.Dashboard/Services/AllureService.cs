namespace Allure.Dashboard.Services;

using Allure.Dashboard.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class AllureService : IAllureService
{
    private readonly ILogger<AllureService> _logger;
    private readonly string _reportsPath;
    private List<TestRun> _cachedTestRuns = new();
    private List<TestResult> _cachedResults = new();
    private List<string> _projects = new();
    private List<string> _tags = new();

    public AllureService(ILogger<AllureService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _reportsPath = configuration["AllureReportsPath"] ?? "./allure-reports";
    }

    public async Task RefreshDataAsync()
    {
        try
        {
            _cachedTestRuns.Clear();
            _cachedResults.Clear();
            _projects.Clear();
            _tags.Clear();

            // Check if reports directory exists
            if (!Directory.Exists(_reportsPath))
            {
                _logger.LogWarning($"Reports path not found: {_reportsPath}");
                return;
            }

            var resultsDir = Path.Combine(_reportsPath, "data", "test-results");
            
            if (Directory.Exists(resultsDir))
            {
                await LoadTestRunsAsync(resultsDir);
            }

            _logger.LogInformation($"Loaded {_cachedTestRuns.Count} test runs with {_cachedResults.Count} test results");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error refreshing data: {ex.Message}");
        }
    }

    private async Task LoadTestRunsAsync(string resultsDir)
    {

        // Load all JSON files from results directory
        var jsonFiles = Directory.GetFiles(resultsDir, "*.json", SearchOption.TopDirectoryOnly);
        
        foreach (var file in jsonFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                var testResults = ParseTestResults(content, file);
                
                if (testResults.Count > 0)
                {
                    // Create a test run from this file
                    var startTime = testResults.Min(r => r.Timestamp);
                    var endTime = testResults.Max(r => r.Timestamp.AddMilliseconds(r.Duration));
                    var runId = Path.GetFileNameWithoutExtension(file);
                    
                    var testRun = new TestRun
                    {
                        Id = runId,
                        Name = $"Test Run - {startTime:yyyy-MM-dd HH:mm:ss}",
                        StartTime = startTime,
                        EndTime = endTime,
                        Results = testResults,
                        PassedCount = testResults.Count(r => r.Status == "PASSED"),
                        FailedCount = testResults.Count(r => r.Status == "FAILED"),
                        SkippedCount = testResults.Count(r => r.Status == "SKIPPED"),
                        BrokenCount = testResults.Count(r => r.Status == "BROKEN")
                    };
                    
                    testRun.PassRate = testRun.PassedCount > 0 ? 
                        (double)testRun.PassedCount / testResults.Count * 100 : 0;
                    
                    _cachedTestRuns.Add(testRun);
                    _cachedResults.AddRange(testResults);
                    
                    // Track projects and tags
                    foreach (var result in testResults)
                    {
                        if (!string.IsNullOrEmpty(result.Project) && !_projects.Contains(result.Project))
                            _projects.Add(result.Project);

                        if (result.Tags != null)
                        {
                            foreach (var tag in result.Tags)
                            {
                                if (!_tags.Contains(tag))
                                    _tags.Add(tag);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not parse file {file}: {ex.Message}");
            }
        }

        // Sort test runs by start time descending
        _cachedTestRuns = _cachedTestRuns.OrderByDescending(r => r.StartTime).ToList();
        _cachedResults = _cachedResults.OrderByDescending(r => r.Timestamp).ToList();
    }

    private List<TestResult> ParseTestResults(string content, string sourceFile)
    {
        var results = new List<TestResult>();
        
        try
        {
            // Try parsing as an array first
            if (content.TrimStart().StartsWith("["))
            {
                var reports = JsonConvert.DeserializeObject<List<TestReport>>(content);
                if (reports != null)
                {
                    foreach (var report in reports)
                    {
                        var result = ConvertReportToResult(report, sourceFile);
                        if (result != null)
                            results.Add(result);
                    }
                    return results;
                }
            }
            else
            {
                // Parse as single object
                var report = JsonConvert.DeserializeObject<TestReport>(content);
                if (report != null)
                {
                    var result = ConvertReportToResult(report, sourceFile);
                    if (result != null)
                        results.Add(result);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Error parsing JSON content: {ex.Message}");
        }
        
        return results;
    }

    private TestResult? ConvertReportToResult(TestReport report, string sourceFile)
    {
        if (report == null || string.IsNullOrEmpty(report.Uuid))
            return null;

        var timestamp = UnixTimeStampToDateTime(report.Start);
        var duration = report.Stop - report.Start;

        return new TestResult
        {
            Id = report.Uuid,
            Name = report.Name,
            HistoryId = report.HistoryId,
            Status = report.Status?.ToUpper() ?? "UNKNOWN",
            Project = ExtractProjectName(report),
            Tags = ExtractTags(report),
            Timestamp = timestamp,
            Duration = duration,
            Source = sourceFile,
            Steps = report.Steps,
            Attachments = report.Attachments
        };
    }

    private DateTime UnixTimeStampToDateTime(long milliseconds)
    {
        var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddMilliseconds(milliseconds).ToLocalTime();
        return dateTime;
    }

    private string ExtractProjectName(TestReport report)
    {
        // Try to extract from titlePath first (most reliable)
        if (report.TitlePath != null && report.TitlePath.Count > 0)
        {
            return report.TitlePath[0];
        }

        // Fall back to extracting from full name
        if (!string.IsNullOrEmpty(report.FullName))
        {
            var parts = report.FullName.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : "Default";
        }

        return "Default";
    }

    private List<string> ExtractTags(TestReport report)
    {
        var tags = new List<string>();
        
        if (report.Labels != null)
        {
            foreach (var label in report.Labels)
            {
                // Look for labels with name "tag"
                if (label.Name == "tag" && !string.IsNullOrEmpty(label.Value))
                {
                    if (!tags.Contains(label.Value))
                        tags.Add(label.Value);
                }
            }
        }

        return tags;
    }

    public async Task<List<TestResult>> GetTestResultsAsync(FilterRequest? filter = null)
    {
        var results = _cachedResults.AsEnumerable();

        if (filter != null)
        {
            if (!string.IsNullOrEmpty(filter.Project))
                results = results.Where(r => r.Project == filter.Project);

            if (filter.Tags?.Count > 0)
                results = results.Where(r => r.Tags != null && r.Tags.Any(t => filter.Tags.Contains(t)));

            if (filter.StartDate.HasValue)
                results = results.Where(r => r.Timestamp >= filter.StartDate.Value);

            if (filter.EndDate.HasValue)
                results = results.Where(r => r.Timestamp <= filter.EndDate.Value);

            if (!string.IsNullOrEmpty(filter.Status))
                results = results.Where(r => r.Status == filter.Status);
        }

        return await Task.FromResult(results.ToList());
    }

    public async Task<List<TestRun>> GetTestRunsAsync(FilterRequest? filter = null)
    {
        var testRuns = _cachedTestRuns.AsEnumerable();

        if (filter != null)
        {
            if (!string.IsNullOrEmpty(filter.Project))
                testRuns = testRuns.Where(r => r.Results?.Any(t => t.Project == filter.Project) ?? false);

            if (filter.Tags?.Count > 0)
                testRuns = testRuns.Where(r => r.Results?.Any(t => t.Tags != null && t.Tags.Any(tag => filter.Tags.Contains(tag))) ?? false);

            if (filter.StartDate.HasValue)
                testRuns = testRuns.Where(r => r.StartTime >= filter.StartDate.Value);

            if (filter.EndDate.HasValue)
                testRuns = testRuns.Where(r => r.EndTime <= filter.EndDate.Value);

            if (!string.IsNullOrEmpty(filter.Status))
                testRuns = testRuns.Where(r => r.Results?.Any(t => t.Status == filter.Status) ?? false);
        }

        return await Task.FromResult(testRuns.ToList());
    }

    public async Task<DashboardData> GetAllDataAsync(FilterRequest? filter = null)
    {
        var results = await GetTestResultsAsync(filter);
        var testRuns = await GetTestRunsAsync(filter);

        var passed = results.Count(r => r.Status == "PASSED");
        var total = results.Count;
        var passRate = total > 0 ? (double)passed / total * 100 : 0;

        return new DashboardData
        {
            TestRuns = testRuns,
            Results = results,
            StatusCounts = new Dictionary<string, int>
            {
                { "PASSED", results.Count(r => r.Status == "PASSED") },
                { "FAILED", results.Count(r => r.Status == "FAILED") },
                { "SKIPPED", results.Count(r => r.Status == "SKIPPED") },
                { "BROKEN", results.Count(r => r.Status == "BROKEN") }
            },
            Projects = _projects,
            AvailableTags = _tags,
            TotalTests = total,
            PassRate = passRate
        };
    }

    public async Task<List<string>> GetProjectsAsync()
    {
        return await Task.FromResult(_projects);
    }

    public async Task<List<string>> GetAvailableTagsAsync()
    {
        return await Task.FromResult(_tags);
    }

    public async Task<List<TimeGroupedTestCase>> GetTestCasesGroupedByTimeAsync()
    {
        var groupedByTime = new Dictionary<string, List<TestResult>>();

        // Group test cases by date and time (truncated to minutes or hour based on preference)
        foreach (var result in _cachedResults.OrderByDescending(r => r.Timestamp))
        {
            // Group by date and hour:minute (e.g., "2025-02-12 15:30")
            var timeKey = result.Timestamp.ToString("yyyy-MM-dd HH:mm");
            
            if (!groupedByTime.ContainsKey(timeKey))
            {
                groupedByTime[timeKey] = new List<TestResult>();
            }
            
            groupedByTime[timeKey].Add(result);
        }

        // Convert to TimeGroupedTestCase objects
        var result_list = groupedByTime.Select(g => new TimeGroupedTestCase
        {
            TimeGroup = g.Key,
            GroupTime = DateTime.ParseExact(g.Key, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            TestCases = g.Value,
            PassedCount = g.Value.Count(t => t.Status == "PASSED"),
            FailedCount = g.Value.Count(t => t.Status == "FAILED"),
            SkippedCount = g.Value.Count(t => t.Status == "SKIPPED"),
            BrokenCount = g.Value.Count(t => t.Status == "BROKEN"),
            PassRate = g.Value.Count > 0 ? (double)g.Value.Count(t => t.Status == "PASSED") / g.Value.Count * 100 : 0
        }).OrderByDescending(x => x.GroupTime).ToList();

        return await Task.FromResult(result_list);
    }
}
