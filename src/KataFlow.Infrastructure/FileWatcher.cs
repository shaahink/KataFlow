using Microsoft.Extensions.Logging;

namespace KataFlow.Infrastructure;

public class FileWatcher : IDisposable
{
    private readonly ILogger<FileWatcher> _logger;
    private FileSystemWatcher? _watcher;
    private readonly TaskCompletionSource<string> _tcs = new();
    private CancellationTokenRegistration _registration;
    private bool _disposed;

    public FileWatcher(ILogger<FileWatcher> logger)
    {
        _logger = logger;
    }

    public async Task<string> WaitForFileAsync(
        string watchDirectory,
        string fileName,
        TimeSpan timeout,
        int pollIntervalMs = 500,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(watchDirectory);

        var existingPath = Path.Combine(watchDirectory, fileName);
        if (File.Exists(existingPath))
            return await File.ReadAllTextAsync(existingPath, ct);

        _registration = ct.Register(() =>
        {
            _tcs.TrySetCanceled(ct);
        });

        _watcher = new FileSystemWatcher(watchDirectory)
        {
            Filter = fileName,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
        };

        _watcher.Created += (_, e) =>
        {
            _logger.LogDebug("FileWatcher detected {File}", e.FullPath);
            _tcs.TrySetResult(e.FullPath);
        };

        var completedTask = await Task.WhenAny(_tcs.Task, Task.Delay(timeout, ct));

        if (completedTask == _tcs.Task)
        {
            var path = await _tcs.Task;
            return await File.ReadAllTextAsync(path, ct);
        }

        for (var elapsed = 0; elapsed < timeout.TotalMilliseconds; elapsed += pollIntervalMs)
        {
            if (File.Exists(existingPath))
                return await File.ReadAllTextAsync(existingPath, ct);
            await Task.Delay(pollIntervalMs, ct);
        }

        throw new TimeoutException($"Timed out waiting for output file: {existingPath}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher?.Dispose();
        _registration.Dispose();
        _tcs.TrySetCanceled();
    }
}
