namespace Allure.Dashboard.Services;

using Allure.Dashboard.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class AllureService : IAllureService
{
    private readonly ILogger<AllureService> _logger;
    private readonly IOracleDataService _oracleDataService;
    private List<TestRun> _cachedTestRuns = new();
    private List<TestResult> _cachedResults = new();
    private List<string> _projects = new();
    private List<string> _tags = new();
    private bool _useOracleDatabase;

    public AllureService(ILogger<AllureService> logger, IConfiguration configuration, IOracleDataService oracleDataService)
    {
        _logger = logger;
        _oracleDataService = oracleDataService;
        _useOracleDatabase = configuration["Database:Provider"] == "Oracle";
    }

    public async Task RefreshDataAsync()
    {
        try
        {
            _cachedTestRuns.Clear();
            _cachedResults.Clear();
            _projects.Clear();
            _tags.Clear();

            if (_useOracleDatabase)
            {
                await LoadFromOracleDatabaseAsync();
            }

            _logger.LogInformation($"Loaded {_cachedTestRuns.Count} test runs with {_cachedResults.Count} test results from {(_useOracleDatabase ? "Oracle Database" : "JSON Files")}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error refreshing data: {ex.Message}");
        }
    }

    private async Task LoadFromOracleDatabaseAsync()
    {
        try
        {
            // Get all Allure results from Oracle
            var allureResults = await _oracleDataService.GetAllureResultsAsync();
            
            if (allureResults.Count == 0)
            {
                _logger.LogWarning("No Allure results found in Oracle database");
                return;
            }

            // Convert Oracle models to TestResult
            foreach (var oracleResult in allureResults)
            {
                var testResult = ConvertOracleResultToTestResult(oracleResult);
                if (testResult != null)
                {
                    // Load steps for this result
                    var steps = await _oracleDataService.GetStepsForResultAsync(oracleResult.Id);
                    if (steps.Count > 0)
                    {
                        testResult.Steps = steps.Select(s => ConvertOracleStepToStep(s)).ToList();
                    }

                    _cachedResults.Add(testResult);
                    
                    // Extract projects and tags
                    if (!string.IsNullOrEmpty(testResult.Project) && !_projects.Contains(testResult.Project))
                        _projects.Add(testResult.Project);

                    if (testResult.Tags != null)
                    {
                        foreach (var tag in testResult.Tags)
                        {
                            if (!_tags.Contains(tag))
                                _tags.Add(tag);
                        }
                    }
                }
            }

            // Get all distinct tags from database
            var distinctTags = await _oracleDataService.GetDistinctTagsAsync();
            foreach (var tag in distinctTags)
            {
                if (!_tags.Contains(tag))
                    _tags.Add(tag);
            }

            // Create test runs from results grouped by date
            var resultsByDate = _cachedResults.GroupBy(r => r.Timestamp.Date);
            foreach (var dateGroup in resultsByDate)
            {
                var results = dateGroup.ToList();
                var startTime = results.Min(r => r.Timestamp);
                var endTime = results.Max(r => r.Timestamp.AddMilliseconds(r.Duration));
                
                var testRun = new TestRun
                {
                    Id = dateGroup.Key.ToString("yyyy-MM-dd"),
                    Name = $"Test Run - {dateGroup.Key:yyyy-MM-dd}",
                    StartTime = startTime,
                    EndTime = endTime,
                    Results = results,
                    PassedCount = results.Count(r => string.Equals(r.Status, "PASSED", StringComparison.CurrentCultureIgnoreCase)),
                    FailedCount = results.Count(r => string.Equals(r.Status, "FAILED", StringComparison.CurrentCultureIgnoreCase)),
                    SkippedCount = results.Count(r => string.Equals(r.Status, "SKIPPED", StringComparison.CurrentCultureIgnoreCase)),
                    BrokenCount = results.Count(r => string.Equals(r.Status, "BROKEN", StringComparison.CurrentCultureIgnoreCase))
                };
                
                testRun.PassRate = testRun.PassedCount > 0 ? 
                    (double)testRun.PassedCount / results.Count * 100 : 0;
                
                _cachedTestRuns.Add(testRun);
            }

            _cachedTestRuns = _cachedTestRuns.OrderByDescending(r => r.StartTime).ToList();
            _cachedResults = _cachedResults.OrderByDescending(r => r.Timestamp).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading data from Oracle database: {ex.Message}");
        }
    }

    private TestResult? ConvertOracleResultToTestResult(OracleAllureResult oracleResult)
    {
        try
        {
            var timestamp = UnixTimeStampToDateTime(oracleResult.StartTime);
            var duration = oracleResult.EndTime - oracleResult.StartTime;

            // Parse labels/tags using format-aware extraction
            var tags = ExtractTagsFromOracleLabels(oracleResult.Labels);

            return new TestResult
            {
                Id = oracleResult.Uuid,
                Name = oracleResult.Name,
                HistoryId = oracleResult.HistoryId,
                Status = oracleResult.Status?.ToUpper() ?? "UNKNOWN",
                Project = oracleResult.Feature ?? "Default",
                Tags = tags.Count > 0 ? tags : null,
                Timestamp = timestamp,
                Duration = duration,
                Source = oracleResult.Uuid,
                Steps = new List<Step>(), // Will be populated from database
                Attachments = null // Will be loaded separately if needed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error converting Oracle result: {ex.Message}");
            return null;
        }
    }

    private List<string> ExtractTagsFromOracleLabels(string? labelsStr)
    {
        var tags = new List<string>();
        
        if (string.IsNullOrEmpty(labelsStr))
            return tags;

        // Try to parse as JSON first (label format: {"name":"tag","value":"sometag"})
        try
        {
            if (labelsStr.Contains("{"))
            {
                // Handle JSON array or single JSON object
                var trimmed = labelsStr.Trim();
                if (!trimmed.StartsWith("["))
                    trimmed = "[" + trimmed + "]";
                
                // Simple regex parsing for name-value pairs in JSON
                var parts = System.Text.RegularExpressions.Regex.Matches(trimmed, @"""value""\s*:\s*""([^""]+)""");
                foreach (System.Text.RegularExpressions.Match match in parts)
                {
                    if (match.Groups.Count > 1)
                        tags.Add(match.Groups[1].Value);
                }
                
                if (tags.Count > 0)
                    return tags;
            }
        }
        catch { }

        // Parse semicolon-separated (e.g., "Smoke; PoliçeSorgulama; All")
        if (labelsStr.Contains(";"))
        {
            var items = labelsStr.Split(';');
            foreach (var item in items)
            {
                var trimmed = item.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    tags.Add(trimmed);
            }
            return tags;
        }

        // Parse pipe-separated (common in Allure: tag1|tag2|tag3)
        if (labelsStr.Contains("|"))
        {
            var items = labelsStr.Split('|');
            foreach (var item in items)
            {
                var trimmed = item.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    tags.Add(trimmed);
            }
            return tags;
        }

        // Parse comma-separated as fallback
        var parts2 = labelsStr.Split(',');
        foreach (var item in parts2)
        {
            var trimmed = item.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                tags.Add(trimmed);
        }

        return tags;
    }

    private Step ConvertOracleStepToStep(OracleAllureStep oracleStep)
    {
        return new Step
        {
            Name = oracleStep.Name,
            Status = oracleStep.Status?.ToLower() ?? "unknown",
            Stage = oracleStep.Stage?.ToLower() ?? "finished",
            Start = oracleStep.StartTime,
            Stop = oracleStep.EndTime,
            Steps = null, // Nested steps not currently supported
            Attachments = null, // Attachments loaded separately if needed
            Parameters = null,
            ScreenshotPath = oracleStep.ScreenshotPath
        };
    }

    private DateTime UnixTimeStampToDateTime(long milliseconds)
    {
        var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddMilliseconds(milliseconds).ToLocalTime();
        return dateTime;
    }

    public async Task<List<TestResult>> GetTestResultsAsync(FilterRequest? filter = null)
    {
        var results = _cachedResults.AsEnumerable();

        if (filter != null)
        {
            if (!string.IsNullOrEmpty(filter.Project))
                results = results.Where(r => r.Project == filter.Project);

            if (filter.Tags?.Count > 0)
                results = results.Where(r => r.Tags != null && filter.Tags.All(t => r.Tags.Contains(t)));

            if (filter.StartDate.HasValue)
                results = results.Where(r => r.Timestamp >= filter.StartDate.Value);

            if (filter.EndDate.HasValue)
                results = results.Where(r => r.Timestamp <= filter.EndDate.Value);

            if (!string.IsNullOrEmpty(filter.Status))
                results = results.Where(r => !string.IsNullOrEmpty(r.Status) && r.Status.Equals(filter.Status, StringComparison.CurrentCultureIgnoreCase));
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
                testRuns = testRuns.Where(r => r.Results?.Any(t => t.Tags != null && filter.Tags.All(tag => t.Tags.Contains(tag))) ?? false);

            if (filter.StartDate.HasValue)
                testRuns = testRuns.Where(r => r.StartTime >= filter.StartDate.Value);

            if (filter.EndDate.HasValue)
                testRuns = testRuns.Where(r => r.EndTime <= filter.EndDate.Value);

            if (!string.IsNullOrEmpty(filter.Status))
                testRuns = testRuns.Where(r => r.Results?.Any(t => !string.IsNullOrEmpty(t.Status) && t.Status.Equals(filter.Status, StringComparison.CurrentCultureIgnoreCase)) ?? false);
        }

        return await Task.FromResult(testRuns.ToList());
    }

    public async Task<DashboardData> GetAllDataAsync(FilterRequest? filter = null)
    {
        var results = await GetTestResultsAsync(filter);
        var testRuns = await GetTestRunsAsync(filter);

        var passed = results.Count(r => string.Equals(r.Status, "PASSED", StringComparison.CurrentCultureIgnoreCase));
        var total = results.Count;
        var passRate = total > 0 ? (double)passed / total * 100 : 0;

        return new DashboardData
        {
            TestRuns = testRuns,
            Results = results,
            StatusCounts = new Dictionary<string, int>
            {
                { "PASSED", results.Count(r => string.Equals(r.Status, "PASSED", StringComparison.CurrentCultureIgnoreCase)) },
                { "FAILED", results.Count(r => string.Equals(r.Status, "FAILED", StringComparison.CurrentCultureIgnoreCase)) },
                { "SKIPPED", results.Count(r => string.Equals(r.Status, "SKIPPED", StringComparison.CurrentCultureIgnoreCase)) },
                { "BROKEN", results.Count(r => string.Equals(r.Status, "BROKEN", StringComparison.CurrentCultureIgnoreCase)) }
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

    public async Task<List<TimeGroupedTestCase>> GetTestCasesGroupedByTimeAsync(FilterRequest? filter = null)
    {
        // First apply filters to get the filtered results
        var filteredResults = _cachedResults.AsEnumerable();

        if (filter != null)
        {
            if (!string.IsNullOrEmpty(filter.Project))
                filteredResults = filteredResults.Where(r => r.Project == filter.Project);

            if (filter.Tags?.Count > 0)
                filteredResults = filteredResults.Where(r => r.Tags != null && filter.Tags.All(t => r.Tags.Contains(t)));

            if (filter.StartDate.HasValue)
                filteredResults = filteredResults.Where(r => r.Timestamp >= filter.StartDate.Value);

            if (filter.EndDate.HasValue)
                filteredResults = filteredResults.Where(r => r.Timestamp <= filter.EndDate.Value);

            if (!string.IsNullOrEmpty(filter.Status))
                filteredResults = filteredResults.Where(r => !string.IsNullOrEmpty(r.Status) && r.Status.Equals(filter.Status, StringComparison.CurrentCultureIgnoreCase));
        }

        var groupedByTime = new Dictionary<string, List<TestResult>>();

        // Group test cases by date and time (truncated to minutes or hour based on preference)
        foreach (var result in filteredResults.OrderByDescending(r => r.Timestamp))
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
            PassedCount = g.Value.Count(t => string.Equals(t.Status, "PASSED", StringComparison.CurrentCultureIgnoreCase)),
            FailedCount = g.Value.Count(t => string.Equals(t.Status, "FAILED", StringComparison.CurrentCultureIgnoreCase)),
            SkippedCount = g.Value.Count(t => string.Equals(t.Status, "SKIPPED", StringComparison.CurrentCultureIgnoreCase)),
            BrokenCount = g.Value.Count(t => string.Equals(t.Status, "BROKEN", StringComparison.CurrentCultureIgnoreCase)),
            PassRate = g.Value.Count > 0 ? (double)g.Value.Count(t => string.Equals(t.Status, "PASSED", StringComparison.CurrentCultureIgnoreCase)) / g.Value.Count * 100 : 0
        }).OrderByDescending(x => x.GroupTime).ToList();

        return await Task.FromResult(result_list);
    }
}
