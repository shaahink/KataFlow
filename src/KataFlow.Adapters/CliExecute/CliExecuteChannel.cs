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

        var finalPrompt = AppendOutputInstructions(request, sessionDir);

        _logger.LogInformation("CliExecute: {Command} {Args} (mode={Mode})",
            _options.Command, _options.Arguments, _options.InputMode);

        try
        {
            BufferedCommandResult result;

            if (_options.InputMode == CliInputMode.Stdin)
            {
                result = await Cli.Wrap(_options.Command)
                    .WithArguments(_options.Arguments)
                    .WithStandardInputPipe(PipeSource.FromString(finalPrompt))
                    .WithWorkingDirectory(_fileSystem.GetCurrentDirectory())
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync(ct);
            }
            else
            {
                var inputFile = _fileSystem.Combine(sessionDir, $"input-{request.StepName}.md");
                await _fileSystem.WriteAllTextAsync(inputFile, finalPrompt, ct);
                _logger.LogInformation("Input file written: {Path}", inputFile);

                var args = _options.Arguments.Contains("{input}", StringComparison.OrdinalIgnoreCase)
                    ? _options.Arguments.Replace("{input}", inputFile, StringComparison.OrdinalIgnoreCase)
                    : $"{_options.Arguments} \"{inputFile}\"";

                result = await Cli.Wrap(_options.Command)
                    .WithArguments(args)
                    .WithWorkingDirectory(_fileSystem.GetCurrentDirectory())
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync(ct);
            }

            var content = string.IsNullOrWhiteSpace(result.StandardOutput)
                ? result.StandardError
                : result.StandardOutput;

            return new AgentResponse
            {
                Content = content,
                Success = result.ExitCode == 0 && !string.IsNullOrWhiteSpace(content),
                ErrorMessage = result.ExitCode != 0
                    ? $"Exit {result.ExitCode}: {result.StandardError}"
                    : null,
                Metadata = new() { ["exit_code"] = result.ExitCode.ToString(), ["command"] = _options.Command },
            };
        }
        catch (OperationCanceledException)
        {
            return new AgentResponse { Content = "", Success = false, ErrorMessage = "CLI execution timed out" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CLI execution failed for step {Step}", request.StepName);
            return new AgentResponse { Content = "", Success = false, ErrorMessage = ex.Message };
        }
    }

    private string AppendOutputInstructions(AgentRequest request, string sessionDir)
    {
        var instructionsPath = _fileSystem.Combine(_templatesPath, "_system", "output-instructions.md");
        if (!_fileSystem.FileExists(instructionsPath))
            return request.RenderedPrompt;

        var outputFile = _fileSystem.Combine(sessionDir, $"output-{request.StepName}.md");
        var vars = new Dictionary<string, string>(request.Metadata)
        {
            ["_output_path"] = outputFile,
            ["_session_id"] = request.SessionId,
            ["_step_name"] = request.StepName,
        };

        var raw = _fileSystem.ReadAllTextAsync(instructionsPath).GetAwaiter().GetResult();
        foreach (var (k, v) in vars)
            raw = raw.Replace($"{{{{{k}}}}}", v, StringComparison.OrdinalIgnoreCase);

        return request.RenderedPrompt + "\n\n" + raw;
    }
}
