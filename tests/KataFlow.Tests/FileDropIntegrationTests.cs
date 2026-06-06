using KataFlow.Adapters.FileDrop;
using KataFlow.Core.Abstractions;
using KataFlow.Core.Models;
using KataFlow.Engine;
using KataFlow.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace KataFlow.Tests;

public class FileDropIntegrationTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly IFileSystem _fs;

    public FileDropIntegrationTests()
    {
        _fs = new SystemFileSystem();
        _fs.CreateDirectory(_fs.Combine(_tempDir, "templates", "_system"));
    }

    [Fact]
    public async Task FileDropChannel_WritesTaskFileWithInstructions()
    {
        var instructions = _fs.Combine(_tempDir, "templates", "_system", "output-instructions.md");
        _fs.WriteAllText(instructions, "Write to {{_output_path}}");

        var logger = Substitute.For<ILogger<FileDropChannel>>();
        var watcherLogger = Substitute.For<ILogger<FileWatcher>>();
        var watcher = new FileWatcher(watcherLogger, _fs);
        var renderer = new PromptRenderer(_fs);

        var channel = new FileDropChannel(
            logger, watcher, renderer, _fs, _fs.Combine(_tempDir, "templates"), 1, 10);

        var sessionDir = _fs.Combine(_tempDir, "sessions", "sess-1");
        _fs.CreateDirectory(sessionDir);

        var request = new AgentRequest
        {
            SessionId = "sess-1",
            StepName = "plan",
            RenderedPrompt = "# Task plan",
        };

        // Write output file first, then watcher picks it up
        _fs.WriteAllText(_fs.Combine(sessionDir, "output-plan.md"), "# Plan result");

        var response = await channel.SendAsync(request, default);

        Assert.True(response.Success);
        Assert.Equal("# Plan result", response.Content);

        var taskFile = _fs.Combine(sessionDir, "task-plan.md");
        Assert.True(_fs.FileExists(taskFile));
        var content = await _fs.ReadAllTextAsync(taskFile);
        Assert.Contains("# Task plan", content);
        Assert.Contains("Write to", content);

        watcher.Dispose();
    }

    [Fact]
    public async Task FileWatcher_DetectsPreExistingFile()
    {
        var logger = Substitute.For<ILogger<FileWatcher>>();
        var watcher = new FileWatcher(logger, _fs);

        var watchDir = _fs.Combine(_tempDir, "pre-existing");
        _fs.CreateDirectory(watchDir);
        _fs.WriteAllText(_fs.Combine(watchDir, "output-done.md"), "# Data");

        var content = await watcher.WaitForFileAsync(
            watchDir, "output-done.md", TimeSpan.FromSeconds(5), 10);

        Assert.Equal("# Data", content);
        watcher.Dispose();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
