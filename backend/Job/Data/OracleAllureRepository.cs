using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using Dapper;
using Job.Models;
using System.Data;

namespace Job.Data;

/// <summary>
/// Repository for persisting Allure test results to Oracle
/// </summary>
public interface IAllureRepository
{
    Task<int> InsertAllureResultAsync(AllureTestResultDb data);
    Task<bool> AllureResultExistsAsync(string uuid);
    Task<IEnumerable<AllureTestResultDb>> GetAllureResultsByFeatureAsync(string feature);
}

public class OracleAllureRepository : IAllureRepository
{
    private readonly string _connectionString;
    private const string TableName = "ALLURE_RESULTS";

    public OracleAllureRepository(IConfiguration configuration)
    {
        var oracleConfig = configuration.GetSection("Oracle");
         _connectionString = oracleConfig["DataSource"];
    }

    public async Task<int> InsertAllureResultAsync(AllureTestResultDb data)
        {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        var query = $@"
        INSERT INTO {TableName} 
        (RUN_ID, UUID, HISTORY_ID, TEST_CASE_ID, FULL_NAME, NAME, STATUS, DURATION_MS, FEATURE, 
         ERROR_MESSAGE, STACK_TRACE, LABELS, STEPS_COUNT, ATTACHMENTS_COUNT, 
         KNOWN, MUTED, FLAKY, START_TIME, END_TIME, CREATED_AT) 
        VALUES 
        (:RunId, :Uuid, :HistoryId, :TestCaseId, :FullName, :Name, :Status, :DurationMs, :Feature, 
         :ErrorMessage, :StackTrace, :Labels, :StepsCount, :AttachmentsCount, 
         :Known, :Muted, :Flaky, :StartTime, :EndTime, SYSDATE)
        RETURNING ID INTO :Id";

        var parameters = new DynamicParameters();

        parameters.Add(":RunId", data.RunId);
        parameters.Add(":Uuid", data.Uuid);
        parameters.Add(":HistoryId", data.HistoryId);
        parameters.Add(":TestCaseId", data.TestCaseId);
        parameters.Add(":FullName", data.FullName);
        parameters.Add(":Name", data.Name);
        parameters.Add(":Status", data.Status);
        parameters.Add(":DurationMs", data.DurationMs);
        parameters.Add(":Feature", data.Feature);
        parameters.Add(":ErrorMessage", data.ErrorMessage);
        parameters.Add(":StackTrace", data.StackTrace);
        parameters.Add(":Labels", data.Labels);
        parameters.Add(":StepsCount", data.StepsCount);
        parameters.Add(":AttachmentsCount", data.AttachmentsCount);
        parameters.Add(":Known", data.Known ? 1 : 0);  // NUMBER(1)
        parameters.Add(":Muted", data.Muted ? 1 : 0);
        parameters.Add(":Flaky", data.Flaky ? 1 : 0);
        parameters.Add(":StartTime", data.StartTime);
        parameters.Add(":EndTime", data.EndTime);

        // OUT param – attention: when getting parameter name use ':' 
        parameters.Add(":Id", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(query, parameters);
        var resultId = parameters.Get<int>(":Id");

        return resultId;
        }

    public async Task<bool> AllureResultExistsAsync(string uuid)
    {
        try
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = $"SELECT COUNT(*) FROM {TableName} WHERE UUID = :uuid";
                var count = await connection.ExecuteScalarAsync<int>(query, new { uuid });
                return count > 0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking Allure result: {ex.Message}");
            return false;
        }
    }

    public async Task<IEnumerable<AllureTestResultDb>> GetAllureResultsByFeatureAsync(string feature)
    {
        try
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = $"SELECT * FROM {TableName} WHERE FEATURE = :feature ORDER BY START_TIME DESC";
                return await connection.QueryAsync<AllureTestResultDb>(query, new { feature });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting Allure results: {ex.Message}");
            return new List<AllureTestResultDb>();
        }
    }
}
