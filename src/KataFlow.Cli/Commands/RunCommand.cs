using System.CommandLine;
using KataFlow.Core.Abstractions;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;

namespace KataFlow.Cli.Commands;

public class RunCommand
{
    private readonly IWorkflowRunner _runner;
    private readonly IWorkflowLoader _loader;
    private readonly ISessionStore _store;
    private readonly IFileSystem _fileSystem;

    public RunCommand(IWorkflowRunner runner, IWorkflowLoader loader, ISessionStore store, IFileSystem fileSystem)
    {
        _runner = runner;
        _loader = loader;
        _store = store;
        _fileSystem = fileSystem;
    }

    public Command Create()
    {
        var command = new Command("run", "Run a workflow");

        var workflowOption = new Option<string>("--workflow")
        {
            Description = "Workflow name (preset) or path to YAML"
        };
        var sessionOption = new Option<string>("--session")
        {
            Description = "Resume a paused session"
        };
        var modeOption = new Option<string>("--mode")
        {
            Description = "Override workflow default mode (dev|headless)"
        };
        var autoApproveOption = new Option<bool>("--auto-approve")
        {
            Description = "Force auto-approve for all steps"
        };
        var varOption = new Option<string[]>("--var")
        {
            Description = "Set a session variable (repeatable)",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };
        var contextOption = new Option<string[]>("--context")
        {
            Description = "Inject file content as a named variable",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };

        command.Add(workflowOption);
        command.Add(sessionOption);
        command.Add(modeOption);
        command.Add(autoApproveOption);
        command.Add(varOption);
        command.Add(contextOption);

        command.SetAction(async (ParseResult parseResult) =>
        {
            var workflow = parseResult.GetValue(workflowOption);
            var sessionId = parseResult.GetValue(sessionOption);
            var modeStr = parseResult.GetValue(modeOption);
            var autoApprove = parseResult.GetValue(autoApproveOption);
            var vars = parseResult.GetValue(varOption) ?? [];
            var contexts = parseResult.GetValue(contextOption) ?? [];

            if (string.IsNullOrEmpty(workflow) && string.IsNullOrEmpty(sessionId))
            {
                Console.Error.WriteLine("Either --workflow or --session is required.");
                return 1;
            }

            var sessionVars = new Dictionary<string, string>();

            foreach (var v in vars)
            {
                var parts = v.Split(['='], 2);
                if (parts.Length == 2)
                    sessionVars[parts[0]] = parts[1];
            }

            foreach (var c in contexts)
            {
                var parts = c.Split(['='], 2);
                if (parts.Length == 2 && _fileSystem.FileExists(parts[1]))
                    sessionVars[parts[0]] = await _fileSystem.ReadAllTextAsync(parts[1]);
            }

            var mode = modeStr?.ToLowerInvariant() switch
            {
                "headless" => OrchestratorMode.Headless,
                _ => OrchestratorMode.Dev
            };

            if (!string.IsNullOrEmpty(workflow))
            {
                var def = _loader.Load(workflow);
                var ctx = new SessionContext
                {
                    Mode = mode,
                    Variables = sessionVars,
                    AutoApprove = autoApprove,
                };

                Console.WriteLine($"Running workflow: {def.Name}");
                if (def.Description is not null)
                    Console.WriteLine(def.Description);

                var result = await _runner.RunAsync(def, ctx);
                Console.WriteLine(result.Success
                    ? $"Session {result.SessionId} completed."
                    : $"Session {result.SessionId} failed: {result.ErrorMessage}");
            }
            else if (!string.IsNullOrEmpty(sessionId))
            {
                var existing = await _store.GetAsync(sessionId);
                if (existing is null)
                {
                    Console.Error.WriteLine($"Session not found: {sessionId}");
                    return 1;
                }

                var def = _loader.Load(existing.WorkflowName);
                var ctx = new SessionContext
                {
                    SessionId = sessionId,
                    Mode = mode,
                    Variables = sessionVars,
                    AutoApprove = autoApprove,
                };

                var result = await _runner.RunAsync(def, ctx);
                Console.WriteLine(result.Success
                    ? $"Session {result.SessionId} completed."
                    : $"Session {result.SessionId} failed: {result.ErrorMessage}");
            }

            return 0;
        });

        return command;
    }
}
