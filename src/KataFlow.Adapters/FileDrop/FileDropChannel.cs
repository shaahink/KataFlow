using KataFlow.Core.Abstractions;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using KataFlow.Infrastructure;
using Microsoft.Extensions.Logging;

namespace KataFlow.Adapters.FileDrop;

public class FileDropChannel : IAgentChannel
{
    private readonly ILogger<FileDropChannel> _logger;
    private readonly FileWatcher _fileWatcher;
    private readonly IPromptRenderer _promptRenderer;
    private readonly IFileSystem _fileSystem;
    private readonly string _templatesPath;
    private readonly int _watchTimeoutMinutes;
    private readonly int _pollIntervalMs;

    public ChannelType Type => ChannelType.FileDrop;

    public FileDropChannel(
        ILogger<FileDropChannel> logger,
        FileWatcher fileWatcher,
        IPromptRenderer promptRenderer,
        IFileSystem fileSystem,
        string templatesPath = "./templates",
        int watchTimeoutMinutes = 15,
        int pollIntervalMs = 500)
    {
        _logger = logger;
        _fileWatcher = fileWatcher;
        _promptRenderer = promptRenderer;
        _fileSystem = fileSystem;
        _templatesPath = templatesPath;
        _watchTimeoutMinutes = watchTimeoutMinutes;
        _pollIntervalMs = pollIntervalMs;
    }

    public async Task<AgentResponse> SendAsync(AgentRequest request, CancellationToken ct = default)
    {
        var sessionDir = _fileSystem.Combine(
            _fileSystem.GetCurrentDirectory(), "sessions", request.SessionId);
        _fileSystem.CreateDirectory(sessionDir);

        var taskFilePath = _fileSystem.Combine(sessionDir, $"task-{request.StepName}.md");
        var outputFileName = $"output-{request.StepName}.md";

        var outputInstructionsPath = _fileSystem.Combine(_templatesPath, "_system", "output-instructions.md");
        var finalPrompt = request.RenderedPrompt;

        if (_fileSystem.FileExists(outputInstructionsPath))
        {
            var outputVars = new Dictionary<string, string>(request.Metadata)
            {
                ["_output_path"] = _fileSystem.Combine(sessionDir, outputFileName),
                ["_session_id"] = request.SessionId,
                ["_step_name"] = request.StepName,
            };
            var instructions = _promptRenderer.Render(outputInstructionsPath, outputVars);
            finalPrompt += "\n\n" + instructions;
        }

        await _fileSystem.WriteAllTextAsync(taskFilePath, finalPrompt, ct);
        _logger.LogInformation("Task file written: {Path}", taskFilePath);

        try
        {
            var content = await _fileWatcher.WaitForFileAsync(
                sessionDir,
                outputFileName,
                TimeSpan.FromMinutes(_watchTimeoutMinutes),
                _pollIntervalMs,
                ct);

            return new AgentResponse
            {
                Content = content,
                Success = !string.IsNullOrWhiteSpace(content),
            };
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"FileDrop timed out waiting for {outputFileName}", ex);
        }
    }
}
