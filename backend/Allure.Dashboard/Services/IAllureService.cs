namespace Allure.Dashboard.Services;

using Allure.Dashboard.Models;

public interface IAllureService
{
    Task<DashboardData> GetAllDataAsync(FilterRequest? filter = null);
    Task<List<TestResult>> GetTestResultsAsync(FilterRequest? filter = null);
    Task<List<string>> GetProjectsAsync();
    Task<List<string>> GetAvailableTagsAsync();
    Task RefreshDataAsync();
    Task<List<TimeGroupedTestCase>> GetTestCasesGroupedByTimeAsync();
}
