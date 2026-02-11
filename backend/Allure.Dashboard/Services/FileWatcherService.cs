namespace Allure.Dashboard.Services;

public class FileWatcherService : IFileWatcherService
{
    private readonly ILogger<FileWatcherService> _logger;
    private readonly IAllureService _allureService;
    private FileSystemWatcher? _watcher;

    public FileWatcherService(ILogger<FileWatcherService> logger, IAllureService allureService)
    {
        _logger = logger;
        _allureService = allureService;
    }

    public void StartWatching(string path)
    {
        try
        {
            // Create directory if it doesn't exist
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            // Initial load
            _allureService.RefreshDataAsync().GetAwaiter().GetResult();

            // Setup file watcher
            _watcher = new FileSystemWatcher(Path.Combine(path, "data", "test-results"))
            {
                Filter = "*.json",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.EnableRaisingEvents = true;

            _logger.LogInformation($"File watcher started for: {path}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error starting file watcher: {ex.Message}");
        }
    }

    public void StopWatching()
    {
        _watcher?.Dispose();
        _logger.LogInformation("File watcher stopped");
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogInformation($"File change detected: {e.Name}");
        
        // Give a small delay to ensure file is fully written
        Task.Delay(500).ContinueWith(_ =>
        {
            _allureService.RefreshDataAsync().GetAwaiter().GetResult();
            _logger.LogInformation("Data refreshed after file change");
        });
    }
}
