namespace Allure.Dashboard.Services;

public interface IFileWatcherService
{
    void StartWatching(string path);
    void StopWatching();
}
