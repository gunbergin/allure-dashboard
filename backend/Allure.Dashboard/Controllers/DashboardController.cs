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
                Projects = !string.IsNullOrEmpty(project) ? project.Split(',').ToList() : null,
                Tags = !string.IsNullOrEmpty(tags) ? tags.Split(',').ToList() : null,
                StartDate = !string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var start) ? start : null,
                EndDate = !string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var end) ? end : null,
                Statuses = !string.IsNullOrEmpty(status) ? status.Split(',').ToList() : null
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
                Projects = !string.IsNullOrEmpty(project) ? project.Split(',').ToList() : null,
                Tags = !string.IsNullOrEmpty(tags) ? tags.Split(',').ToList() : null,
                StartDate = !string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var start) ? start : null,
                EndDate = !string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var end) ? end : null,
                Statuses = !string.IsNullOrEmpty(status) ? status.Split(',').ToList() : null
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

    [HttpGet("test-cases-by-time")]
    public async Task<IActionResult> GetTestCasesGroupedByTime([FromQuery] string? project, [FromQuery] string? tags, [FromQuery] string? startDate, [FromQuery] string? endDate, [FromQuery] string? status)
    {
        try
        {
            var filter = new FilterRequest
            {
                Projects = !string.IsNullOrEmpty(project) ? project.Split(',').ToList() : null,
                Tags = !string.IsNullOrEmpty(tags) ? tags.Split(',').ToList() : null,
                StartDate = !string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var start) ? start : null,
                EndDate = !string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var end) ? end : null,
                Statuses = !string.IsNullOrEmpty(status) ? status.Split(',').ToList() : null
            };

            var groupedTestCases = await _allureService.GetTestCasesGroupedByTimeAsync(filter);
            return Ok(groupedTestCases);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting test cases grouped by time: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("attachment/{*path}")]
    public IActionResult GetAttachment(string path)
    {
        try
        {
            var basePath = Directory.Exists("../../allure-reports") 
                ? Path.GetFullPath("../../allure-reports") 
                : Path.GetFullPath("./allure-reports");
            
            var fullPath = Path.GetFullPath(Path.Combine(basePath, path));
            
            // Prevent directory traversal attacks
            if (!fullPath.StartsWith(basePath))
            {
                return Unauthorized();
            }

            if (!System.IO.File.Exists(fullPath))
            {
                _logger.LogWarning($"Attachment file not found: {fullPath}");
                return NotFound();
            }

            var contentType = GetContentType(fullPath);
            var fileStream = System.IO.File.OpenRead(fullPath);
            return File(fileStream, contentType, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error serving attachment: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private string GetContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        // If no extension, try to detect by reading the file magic bytes or checking Allure conventions
        if (string.IsNullOrEmpty(extension))
        {
            try
            {
                using (var fs = System.IO.File.OpenRead(filePath))
                {
                    byte[] buffer = new byte[4];
                    fs.Read(buffer, 0, 4);
                    
                    // Check PNG magic bytes: 89 50 4E 47
                    if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
                        return "image/png";
                    
                    // Check JPEG magic bytes: FF D8 FF
                    if (buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
                        return "image/jpeg";
                    
                    // Check GIF magic bytes: 47 49 46 38
                    if (buffer[0] == 0x47 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x38)
                        return "image/gif";
                }
            }
            catch { }
        }
        
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }}