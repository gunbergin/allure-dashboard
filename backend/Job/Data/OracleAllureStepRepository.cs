using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using Dapper;
using Job.Models;

namespace Job.Data;

/// <summary>
/// Repository for persisting Allure test steps to Oracle
/// </summary>
public interface IAllureStepRepository
{   
    Task<int> InsertAllureStepsAsync(IEnumerable<AllureStepDb> steps);
 
}

public class OracleAllureStepRepository : IAllureStepRepository
{
    private readonly string _connectionString;
    private const string TableName = "ALLURE_STEPS";

    public OracleAllureStepRepository(IConfiguration configuration)
    {
        var oracleConfig = configuration.GetSection("Oracle");
        var dataSource = oracleConfig["DataSource"];

        if (string.IsNullOrWhiteSpace(dataSource))
        {
            throw new InvalidOperationException("Oracle configuration is missing");
        }

        _connectionString = dataSource;
        }

   

    public async Task<int> InsertAllureStepsAsync(IEnumerable<AllureStepDb> steps)
    {
        try
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();

                var stepList = steps.ToList();
                Console.WriteLine($"[OracleAllureStepRepository] Batch inserting {stepList.Count} steps");
                
                foreach (var step in stepList)
                {
                    Console.WriteLine($"[OracleAllureStepRepository]   Step: {step.Name} | Status: {step.Status} | Screenshot: {(step.ScreenshotBlob == null ? "NULL" : $"{step.ScreenshotBlob.Length} bytes")}");
                }

                var query = $@"
                    INSERT INTO {TableName} 
                    (ALLURE_RESULT_ID, NAME, STATUS, START_TIME, END_TIME, DURATION_MS, STAGE, 
                     MESSAGE, TRACE, ATTACHMENTS_COUNT, NESTED_STEPS_COUNT, SCREENSHOT_BLOB, CREATED_AT) 
                    VALUES 
                    (:AllureResultId, :Name, :Status, :StartTime, :EndTime, :DurationMs, :Stage, 
                     :Message, :Trace, :AttachmentsCount, :NestedStepsCount, :ScreenshotBlob, SYSDATE)";

                var result = await connection.ExecuteAsync(query, stepList);
                Console.WriteLine($"[OracleAllureStepRepository] ✓ Batch inserted {stepList.Count} steps (affected rows: {result})");
                return result;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OracleAllureStepRepository] ERROR batch inserting steps: {ex.Message}");
            Console.WriteLine($"[OracleAllureStepRepository] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

}
