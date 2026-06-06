using KataFlow.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace KataFlow.Infrastructure;

public class FileWatcher : IDisposable
{
    private readonly ILogger<FileWatcher> _logger;
    private readonly IFileSystem _fileSystem;
    private FileSystemWatcher? _watcher;
    private TaskCompletionSource<string>? _tcs;
    private CancellationTokenRegistration _registration;
    private bool _disposed;

    public FileWatcher(ILogger<FileWatcher> logger, IFileSystem fileSystem)
    {
        _logger = logger;
        _fileSystem = fileSystem;
    }

    public async Task<string> WaitForFileAsync(
        string watchDirectory,
        string fileName,
        TimeSpan timeout,
        int pollIntervalMs = 500,
        CancellationToken ct = default)
    {
        _fileSystem.CreateDirectory(watchDirectory);

        var existingPath = _fileSystem.Combine(watchDirectory, fileName);
        if (_fileSystem.FileExists(existingPath))
            return await _fileSystem.ReadAllTextAsync(existingPath, ct);

        _tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        _registration = ct.Register(() => _tcs.TrySetCanceled(ct));

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
            CleanupWatcher();
            return await _fileSystem.ReadAllTextAsync(path, ct);
        }

        CleanupWatcher();

        for (var elapsed = 0; elapsed < timeout.TotalMilliseconds; elapsed += pollIntervalMs)
        {
            if (_fileSystem.FileExists(existingPath))
                return await _fileSystem.ReadAllTextAsync(existingPath, ct);
            await Task.Delay(pollIntervalMs, ct);
        }

        throw new TimeoutException($"Timed out waiting for output file: {existingPath}");
    }

    private void CleanupWatcher()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CleanupWatcher();
        _registration.Dispose();
        _tcs?.TrySetCanceled();
    }
}
