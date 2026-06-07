using CliWrap;
using CliWrap.Buffered;
using KataFlow.Core.Abstractions;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KataFlow.Adapters.CliExecute;

public class CliExecuteChannel : IAgentChannel
{
    private readonly ILogger<CliExecuteChannel> _logger;
    private readonly IFileSystem _fileSystem;
    private readonly CliExecuteOptions _options;
    private readonly string _templatesPath;

    public ChannelType Type => ChannelType.CliExecute;

    public CliExecuteChannel(
        ILogger<CliExecuteChannel> logger,
        IFileSystem fileSystem,
        IOptions<CliExecuteOptions> options,
        string templatesPath = "./templates")
    {
        _logger = logger;
        _fileSystem = fileSystem;
        _options = options.Value;
        _templatesPath = templatesPath;
    }

    public async Task<AgentResponse> SendAsync(AgentRequest request, CancellationToken ct = default)
    {
        var sessionDir = _fileSystem.Combine(
            _fileSystem.GetCurrentDirectory(), "sessions", request.SessionId);
        _fileSystem.CreateDirectory(sessionDir);

        var inputFile = _fileSystem.Combine(sessionDir, $"input-{request.StepName}.md");
        var finalPrompt = request.RenderedPrompt;

        // Append output instructions if available
        var instructionsPath = _fileSystem.Combine(_templatesPath, "_system", "output-instructions.md");
        if (_fileSystem.FileExists(instructionsPath))
        {
            var outputFile = _fileSystem.Combine(sessionDir, $"output-{request.StepName}.md");
            var vars = new Dictionary<string, string>(request.Metadata)
            {
                ["_output_path"] = outputFile,
                ["_session_id"] = request.SessionId,
                ["_step_name"] = request.StepName,
            };
            var instructions = string.Join("\n", System.IO.File.ReadLines(instructionsPath));
            // Simple variable replacement for instructions
            foreach (var (k, v) in vars)
                instructions = instructions.Replace($"{{{{{k}}}}}", v);
            finalPrompt += "\n\n" + instructions;
        }

        await _fileSystem.WriteAllTextAsync(inputFile, finalPrompt, ct);
        _logger.LogInformation("Input file written: {Path}", inputFile);

        var args = _options.ArgumentsTemplate
            .Replace("{input}", inputFile)
            .Replace("{session}", request.SessionId)
            .Replace("{step}", request.StepName);

        _logger.LogInformation("Executing: {Command} {Args}", _options.Command, args);

        try
        {
            var result = await Cli.Wrap(_options.Command)
                .WithArguments(args)
                .WithWorkingDirectory(_fileSystem.GetCurrentDirectory())
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(ct);

            var content = result.StandardOutput;

            if (string.IsNullOrWhiteSpace(content))
                content = result.StandardError;

            return new AgentResponse
            {
                Content = content,
                Success = result.ExitCode == 0 && !string.IsNullOrWhiteSpace(content),
                ErrorMessage = result.ExitCode != 0
                    ? $"Exit code {result.ExitCode}: {result.StandardError}"
                    : null,
                Metadata = new()
                {
                    ["exit_code"] = result.ExitCode.ToString(),
                    ["command"] = _options.Command,
                },
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("CLI execution timed out for step {Step}", request.StepName);
            return new AgentResponse
            {
                Content = "",
                Success = false,
                ErrorMessage = "CLI execution timed out",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CLI execution failed for step {Step}", request.StepName);
            return new AgentResponse
            {
                Content = "",
                Success = false,
                ErrorMessage = ex.Message,
            };
        }
    }
}
