using Allure.Dashboard.Models;
using Allure.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace Allure.Dashboard.Controllers;

[ApiController]
[Route("api")]
public class DashboardController : ControllerBase
{
    private readonly IAllureService _allureService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IAllureService allureService, ILogger<DashboardController> logger)
    {
        _allureService = allureService;
        _logger = logger;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] string? project, [FromQuery] string? tags, [FromQuery] string? startDate, [FromQuery] string? endDate, [FromQuery] string? status)
    {
        try
        {
            var filter = new FilterRequest
            {
                Project = project,
                Tags = !string.IsNullOrEmpty(tags) ? tags.Split(',').ToList() : null,
                StartDate = !string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var start) ? start : null,
                EndDate = !string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var end) ? end : null,
                Status = status
            };

            var data = await _allureService.GetAllDataAsync(filter);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting dashboard data: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("results")]
    public async Task<IActionResult> GetResults([FromQuery] string? project, [FromQuery] string? tags, [FromQuery] string? startDate, [FromQuery] string? endDate, [FromQuery] string? status)
    {
        try
        {
            var filter = new FilterRequest
            {
                Project = project,
                Tags = !string.IsNullOrEmpty(tags) ? tags.Split(',').ToList() : null,
                StartDate = !string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var start) ? start : null,
                EndDate = !string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var end) ? end : null,
                Status = status
            };

            var results = await _allureService.GetTestResultsAsync(filter);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting results: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects()
    {
        try
        {
            var projects = await _allureService.GetProjectsAsync();
            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting projects: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("tags")]
    public async Task<IActionResult> GetTags()
    {
        try
        {
            var tags = await _allureService.GetAvailableTagsAsync();
            return Ok(tags);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting tags: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshData()
    {
        try
        {
            await _allureService.RefreshDataAsync();
            return Ok(new { message = "Data refreshed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error refreshing data: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }
}
