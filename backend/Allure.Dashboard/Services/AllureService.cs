namespace Allure.Dashboard.Services;

using Allure.Dashboard.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class AllureService : IAllureService
{
    private readonly ILogger<AllureService> _logger;
    private readonly string _reportsPath;
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
            _cachedResults.Clear();
            _projects.Clear();
            _tags.Clear();

            // Check if reports directory exists
            if (!Directory.Exists(_reportsPath))
            {
                _logger.LogWarning($"Reports path not found: {_reportsPath}");
                return;
            }

            // Look for test-cases directory or json files
            var testCasesDir = Path.Combine(_reportsPath, "data", "test-cases");
            var resultsDir = Path.Combine(_reportsPath, "data", "test-results");

            // Load history.json for metadata
            var historyFile = Path.Combine(_reportsPath, "data", "history.json");
            
            if (Directory.Exists(resultsDir))
            {
                await LoadTestResultsAsync(resultsDir, historyFile);
            }

            _logger.LogInformation($"Loaded {_cachedResults.Count} test results");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error refreshing data: {ex.Message}");
        }
    }

    private async Task LoadTestResultsAsync(string resultsDir, string historyFile)
    {
        var historyData = new Dictionary<string, JObject>();
        
        // Load history data if it exists
        if (File.Exists(historyFile))
        {
            try
            {
                var historyContent = await File.ReadAllTextAsync(historyFile);
                if (!string.IsNullOrEmpty(historyContent))
                {
                    historyData = JsonConvert.DeserializeObject<Dictionary<string, JObject>>(historyContent) ?? new();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not read history file: {ex.Message}");
            }
        }

        // Load all JSON files from results directory
        var jsonFiles = Directory.GetFiles(resultsDir, "*.json", SearchOption.TopDirectoryOnly);
        
        foreach (var file in jsonFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                var report = JsonConvert.DeserializeObject<TestReport>(content);

                if (report != null && !string.IsNullOrEmpty(report.Uuid))
                {
                    // Convert milliseconds to DateTime (Unix milliseconds)
                    var timestamp = UnixTimeStampToDateTime(report.Start);
                    var duration = report.Stop - report.Start;

                    var result = new TestResult
                    {
                        Id = report.Uuid,
                        Name = report.Name,
                        HistoryId = report.HistoryId,
                        Status = report.Status?.ToUpper() ?? "UNKNOWN",
                        Project = ExtractProjectName(report),
                        Tags = ExtractTags(report),
                        Timestamp = timestamp,
                        Duration = duration,
                        Source = file,
                        Steps = report.Steps
                    };

                    _cachedResults.Add(result);

                    // Track projects and tags
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
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not parse file {file}: {ex.Message}");
            }
        }

        // Sort by timestamp descending
        _cachedResults = _cachedResults.OrderByDescending(r => r.Timestamp).ToList();
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

    public async Task<DashboardData> GetAllDataAsync(FilterRequest? filter = null)
    {
        var results = await GetTestResultsAsync(filter);

        var passed = results.Count(r => r.Status == "PASSED");
        var total = results.Count;
        var passRate = total > 0 ? (double)passed / total * 100 : 0;

        return new DashboardData
        {
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
}
