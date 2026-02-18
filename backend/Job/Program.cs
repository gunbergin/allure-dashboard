using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Job;
using Job.Data;
using Job.Services;
using Job.Jobs;

namespace Job;

public class Program
{
    public static void Main(string[] args)
    {
#if DEBUG
        // Geliþtirici makinesinde F5 ile konsol gibi çalýþtýr
        CreateHostBuilder(args).Build().Run();


#else
        // Gerçek ortamda Windows servisi olarak çalýþtýr
        CreateHostBuilder(args).Build().RunAsService();
#endif
       
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService()
            .ConfigureServices((hostContext, services) =>
            {
                var env = hostContext.HostingEnvironment.EnvironmentName;

                // Load configuration
                var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{env}.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();

                // Register configuration
                services.AddSingleton(configuration);

                // Register logging
                services.AddLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                });

               
                
                // Register Allure repositories and services
                services.AddScoped<IAllureRepository, OracleAllureRepository>();
                services.AddScoped<IAllureStepRepository, OracleAllureStepRepository>();
                services.AddScoped<IAllureJsonService, AllureJsonService>();

                // Register background job for processing Allure JSON files
                services.AddScoped<IAllureJsonJob, AllureJsonJob>();



                // Register Quartz scheduler
                services.AddQuartz(q =>
                {
                    // Register the job
                    var jobKey = new JobKey("AllureJsonProcessingJob");
                    q.AddJob<AllureJsonProcessingJob>(opts => opts.WithIdentity(jobKey));


#if DEBUG
                    // SADECE debug için: cron'u geçici olarak kapat, sadece StartNow kullan
                    q.AddTrigger(opts => opts
                        .ForJob(jobKey)
                        .WithIdentity("AllureJsonProcessingTrigger_Startup")
                        .StartNow()
                        .WithSimpleSchedule(x => x.WithRepeatCount(0)));
#else
    // Release: yalnýzca günlük cron
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("AllureJsonProcessingTrigger")
        .WithCronSchedule("0 0 0 * * ?"));
#endif
                });

                services.AddQuartzHostedService(options =>
                {
                    options.WaitForJobsToComplete = true;
                });


            });
}
