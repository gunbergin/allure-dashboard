using Allure.Dashboard.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddSingleton<IOracleDataService, OracleDataService>();
builder.Services.AddSingleton<IAllureService, AllureService>();
builder.Services.AddSingleton<IFileWatcherService, FileWatcherService>();

var app = builder.Build();

// Serve static files from frontend directory
var frontendPath = Path.Combine(app.Environment.ContentRootPath, "../../frontend");
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.GetFullPath(frontendPath)),
    RequestPath = ""
});

app.UseRouting();
app.UseCors("AllowAll");

app.MapControllers();

// Fallback to index.html for SPA
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.GetFullPath(frontendPath)),
});

var fileWatcher = app.Services.GetRequiredService<IFileWatcherService>();
fileWatcher.StartWatching(builder.Configuration["AllureReportsPath"] ?? "../../allure-reports");

app.Run();
