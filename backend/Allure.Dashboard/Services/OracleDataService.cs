namespace Allure.Dashboard.Services;

using Allure.Dashboard.Models;
using Oracle.ManagedDataAccess.Client;

public interface IOracleDataService
{
    Task<List<OracleAllureResult>> GetAllureResultsAsync();
    Task<List<OracleAllureResult>> GetAllureResultsByFeatureAsync(string feature);
    Task<List<OracleAllureResult>> GetAllureResultsByStatusAsync(string status);
    Task<List<OracleAllureStep>> GetStepsForResultAsync(long resultId);
    Task<List<string>> GetDistinctFeaturesAsync();
    Task<List<string>> GetDistinctLabelsAsync();
    Task<List<string>> GetDistinctTagsAsync();
}

public class OracleDataService : IOracleDataService
{
    private readonly ILogger<OracleDataService> _logger;
    private readonly string _connectionString;

    public OracleDataService(ILogger<OracleDataService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = configuration["Database:ConnectionString"] ?? 
            "Data Source=localhost:1521/xe;User Id=nova_user;Password=password;";
    }

    public async Task<List<OracleAllureResult>> GetAllureResultsAsync()
    {
        var results = new List<OracleAllureResult>();
        var query = @"
            SELECT ID, UUID, HISTORY_ID, TEST_CASE_ID, FULL_NAME, NAME, STATUS, 
                   DURATION_MS, FEATURE, ERROR_MESSAGE, STACK_TRACE, LABELS, 
                   STEPS_COUNT, ATTACHMENTS_COUNT, KNOWN, MUTED, FLAKY, 
                   START_TIME, END_TIME, CREATED_AT
            FROM ALLURE_RESULTS
            ORDER BY START_TIME DESC";

        try
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(query, connection))
                {
                    command.CommandType = System.Data.CommandType.Text;
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(MapReaderToAllureResult(reader));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching Allure results: {ex.Message}");
        }

        return results;
    }

    public async Task<List<OracleAllureResult>> GetAllureResultsByFeatureAsync(string feature)
    {
        var results = new List<OracleAllureResult>();
        var query = @"
            SELECT ID, UUID, HISTORY_ID, TEST_CASE_ID, FULL_NAME, NAME, STATUS, 
                   DURATION_MS, FEATURE, ERROR_MESSAGE, STACK_TRACE, LABELS, 
                   STEPS_COUNT, ATTACHMENTS_COUNT, KNOWN, MUTED, FLAKY, 
                   START_TIME, END_TIME, CREATED_AT
            FROM ALLURE_RESULTS
            WHERE FEATURE LIKE :feature
            ORDER BY START_TIME DESC";

        try
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(query, connection))
                {
                    command.Parameters.Add("feature", $"%{feature}%");
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(MapReaderToAllureResult(reader));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching Allure results by feature: {ex.Message}");
        }

        return results;
    }

    public async Task<List<OracleAllureResult>> GetAllureResultsByStatusAsync(string status)
    {
        var results = new List<OracleAllureResult>();
        var query = @"
            SELECT ID, UUID, HISTORY_ID, TEST_CASE_ID, FULL_NAME, NAME, STATUS, 
                   DURATION_MS, FEATURE, ERROR_MESSAGE, STACK_TRACE, LABELS, 
                   STEPS_COUNT, ATTACHMENTS_COUNT, KNOWN, MUTED, FLAKY, 
                   START_TIME, END_TIME, CREATED_AT
            FROM ALLURE_RESULTS
            WHERE STATUS = :status
            ORDER BY START_TIME DESC";

        try
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(query, connection))
                {
                    command.Parameters.Add("status", status);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(MapReaderToAllureResult(reader));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching Allure results by status: {ex.Message}");
        }

        return results;
    }

    public async Task<List<OracleAllureStep>> GetStepsForResultAsync(long resultId)
    {
        var steps = new List<OracleAllureStep>();
        var query = @"
            SELECT ID, ALLURE_RESULT_ID, NAME, STATUS, START_TIME, END_TIME, 
                   DURATION_MS, STAGE, MESSAGE, TRACE, ATTACHMENTS_COUNT, 
                   NESTED_STEPS_COUNT, CREATED_AT
            FROM ALLURE_STEPS
            WHERE ALLURE_RESULT_ID = :resultId
            ORDER BY START_TIME ASC";

        try
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(query, connection))
                {
                    command.Parameters.Add("resultId", resultId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            steps.Add(MapReaderToAllureStep(reader));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching steps for result {resultId}: {ex.Message}");
        }

        return steps;
    }

    public async Task<List<string>> GetDistinctFeaturesAsync()
    {
        var features = new List<string>();
        var query = "SELECT DISTINCT FEATURE FROM ALLURE_RESULTS WHERE FEATURE IS NOT NULL ORDER BY FEATURE";

        try
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(query, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            if (!reader.IsDBNull(0))
                                features.Add(reader.GetString(0));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching distinct features: {ex.Message}");
        }

        return features;
    }

    public async Task<List<string>> GetDistinctLabelsAsync()
    {
        var labels = new List<string>();
        var query = "SELECT DISTINCT LABELS FROM ALLURE_RESULTS WHERE LABELS IS NOT NULL";

        try
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(query, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                var labelStr = reader.GetString(0);
                                var parsedLabels = ParseLabelsFromString(labelStr);
                                foreach (var label in parsedLabels)
                                {
                                    if (!string.IsNullOrEmpty(label) && !labels.Contains(label))
                                        labels.Add(label);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching distinct labels: {ex.Message}");
        }

        return labels;
    }

    public async Task<List<string>> GetDistinctTagsAsync()
    {
        // Tags are stored in LABELS field in ALLURE_RESULTS, so use the same logic
        return await GetDistinctLabelsAsync();
    }

    private List<string> ParseLabelsFromString(string labelStr)
    {
        var labels = new List<string>();
        
        if (string.IsNullOrEmpty(labelStr))
            return labels;

        // Try to parse as JSON first (label format: {"name":"tag","value":"sometag"})
        try
        {
            if (labelStr.Contains("{"))
            {
                // Handle JSON array or single JSON object
                var trimmed = labelStr.Trim();
                if (!trimmed.StartsWith("["))
                    trimmed = "[" + trimmed + "]";
                
                // Simple regex parsing for name-value pairs in JSON
                var parts = System.Text.RegularExpressions.Regex.Matches(trimmed, @"""value""\s*:\s*""([^""]+)""");
                foreach (System.Text.RegularExpressions.Match match in parts)
                {
                    if (match.Groups.Count > 1)
                        labels.Add(match.Groups[1].Value);
                }
                
                if (labels.Count > 0)
                    return labels;
            }
        }
        catch { }

        // Parse semicolon-separated (e.g., "Smoke; PoliçeSorgulama; All")
        if (labelStr.Contains(";"))
        {
            var items = labelStr.Split(';');
            foreach (var item in items)
            {
                var trimmed = item.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    labels.Add(trimmed);
            }
            return labels;
        }

        // Parse pipe-separated (common in Allure: tag1|tag2|tag3)
        if (labelStr.Contains("|"))
        {
            var items = labelStr.Split('|');
            foreach (var item in items)
            {
                var trimmed = item.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    labels.Add(trimmed);
            }
            return labels;
        }

        // Parse comma-separated as fallback
        var parts2 = labelStr.Split(',');
        foreach (var item in parts2)
        {
            var trimmed = item.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                labels.Add(trimmed);
        }

        return labels;
    }




    private OracleAllureResult MapReaderToAllureResult(OracleDataReader reader)
    {
        return new OracleAllureResult
        {
            Id = reader.GetInt64(0),
            Uuid = reader.IsDBNull(1) ? null : reader.GetString(1),
            HistoryId = reader.IsDBNull(2) ? null : reader.GetString(2),
            TestCaseId = reader.IsDBNull(3) ? null : reader.GetString(3),
            FullName = reader.IsDBNull(4) ? null : reader.GetString(4),
            Name = reader.IsDBNull(5) ? null : reader.GetString(5),
            Status = reader.IsDBNull(6) ? null : reader.GetString(6),
            DurationMs = reader.IsDBNull(7) ? null : reader.GetInt64(7),
            Feature = reader.IsDBNull(8) ? null : reader.GetString(8),
            ErrorMessage = reader.IsDBNull(9) ? null : reader.GetString(9),
            StackTrace = reader.IsDBNull(10) ? null : reader.GetString(10),
            Labels = reader.IsDBNull(11) ? null : reader.GetString(11),
            StepsCount = reader.IsDBNull(12) ? 0 : (int)reader.GetInt64(12),
            AttachmentsCount = reader.IsDBNull(13) ? 0 : (int)reader.GetInt64(13),
            Known = !reader.IsDBNull(14) && reader.GetInt64(14) == 1,
            Muted = !reader.IsDBNull(15) && reader.GetInt64(15) == 1,
            Flaky = !reader.IsDBNull(16) && reader.GetInt64(16) == 1,
            StartTime = reader.GetInt64(17),
            EndTime = reader.GetInt64(18),
            CreatedAt = reader.IsDBNull(19) ? DateTime.Now : reader.GetDateTime(19)
        };
    }

    private OracleAllureStep MapReaderToAllureStep(OracleDataReader reader)
    {
        return new OracleAllureStep
        {
            Id = reader.GetInt64(0),
            AllureResultId = reader.GetInt64(1),
            Name = reader.IsDBNull(2) ? null : reader.GetString(2),
            Status = reader.IsDBNull(3) ? null : reader.GetString(3),
            StartTime = reader.GetInt64(4),
            EndTime = reader.GetInt64(5),
            DurationMs = reader.GetInt64(6),
            Stage = reader.IsDBNull(7) ? null : reader.GetString(7),
            Message = reader.IsDBNull(8) ? null : reader.GetString(8),
            Trace = reader.IsDBNull(9) ? null : reader.GetString(9),
            AttachmentsCount = reader.IsDBNull(10) ? 0 : (int)reader.GetInt64(10),
            NestedStepsCount = reader.IsDBNull(11) ? 0 : (int)reader.GetInt64(11),
            CreatedAt = reader.IsDBNull(12) ? DateTime.Now : reader.GetDateTime(12)
        };
    }
}
