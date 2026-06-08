using System.CommandLine;
using System.Text.Json;
using KataFlow.Core.Abstractions;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using Spectre.Console;

namespace KataFlow.Cli.Commands;

public class RunCommand
{
    private readonly IWorkflowRunner _runner;
    private readonly IWorkflowLoader _loader;
    private readonly IFileSystem _fileSystem;

    public RunCommand(IWorkflowRunner runner, IWorkflowLoader loader, ISessionStore store, IFileSystem fileSystem)
    {
        _runner = runner;
        _loader = loader;
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
        var budgetCapOption = new Option<decimal?>("--budget-cap")
        {
            Description = "Warn when session cost exceeds this USD amount"
        };

        command.Add(workflowOption);
        command.Add(sessionOption);
        command.Add(modeOption);
        command.Add(autoApproveOption);
        command.Add(varOption);
        command.Add(contextOption);
        command.Add(budgetCapOption);

        command.SetAction(async (ParseResult parseResult) =>
        {
            var workflow = parseResult.GetValue(workflowOption);
            var sessionId = parseResult.GetValue(sessionOption);
            var modeStr = parseResult.GetValue(modeOption);
            var autoApprove = parseResult.GetValue(autoApproveOption);
            var vars = parseResult.GetValue(varOption) ?? [];
            var contexts = parseResult.GetValue(contextOption) ?? [];
            var budgetCap = parseResult.GetValue(budgetCapOption);

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

            using var cts = new CancellationTokenSource();

            if (!string.IsNullOrEmpty(workflow))
            {
                var def = _loader.Load(workflow);
                var ctx = new SessionContext
                {
                    Mode = mode,
                    Variables = sessionVars,
                    AutoApprove = autoApprove,
                    BudgetCapUsd = budgetCap,
                };

                SessionResult result = null!;
                var runTask = _runner.RunAsync(def, ctx, cts.Token);

                var runId = def.Name;
                var sessionFile = FindSessionFile(runId);
                var started = false;

                await AnsiConsole.Live(CreateStatusTable(def.Name, "Starting..."))
                    .StartAsync(async liveCtx =>
                    {
                        while (!runTask.IsCompleted)
                        {
                            if (!started && sessionFile is not null)
                            {
                                AnsiConsole.MarkupLine($"[dim]Session ID: [/]{Path.GetFileName(Path.GetDirectoryName(sessionFile)!)}");
                                AnsiConsole.MarkupLine($"[dim]Watch from another terminal: [/]kataflow watch --session {Path.GetFileName(Path.GetDirectoryName(sessionFile)!)}");
                                started = true;
                            }

                            sessionFile ??= FindSessionFile(runId);

                            if (sessionFile is not null && _fileSystem.FileExists(sessionFile))
                            {
                                try
                                {
                                    var json = await _fileSystem.ReadAllTextAsync(sessionFile);
                                    var session = JsonSerializer.Deserialize<Session>(json,
                                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                    if (session is not null)
                                        liveCtx.UpdateTarget(CreateStatusTable(session));
                                }
                                catch { }
                            }
                            await Task.Delay(1000);
                        }

                        result = await runTask;
                    });

                Console.WriteLine(result.Success
                    ? $"Session {result.SessionId} completed."
                    : $"Session {result.SessionId} failed: {result.ErrorMessage}");
            }
            else if (!string.IsNullOrEmpty(sessionId))
            {
                var existingJson = _fileSystem.ReadAllTextAsync(
                    _fileSystem.Combine(_fileSystem.GetCurrentDirectory(), "sessions", sessionId, "session.json")).GetAwaiter().GetResult();
                var existing = JsonSerializer.Deserialize<Session>(existingJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidOperationException($"Session not found: {sessionId}");

                var def = _loader.Load(existing.WorkflowName);
                var ctx = new SessionContext
                {
                    SessionId = sessionId,
                    Mode = mode,
                    Variables = sessionVars,
                    AutoApprove = autoApprove,
                    BudgetCapUsd = budgetCap,
                };

                SessionResult result = null!;
                var runTask = _runner.RunAsync(def, ctx, cts.Token);
                var sessionFile = _fileSystem.Combine(_fileSystem.GetCurrentDirectory(), "sessions", sessionId, "session.json");

                await AnsiConsole.Live(CreateStatusTable(def.Name, "Resuming..."))
                    .StartAsync(async liveCtx =>
                    {
                        while (!runTask.IsCompleted)
                        {
                            if (_fileSystem.FileExists(sessionFile))
                            {
                                try
                                {
                                    var json = await _fileSystem.ReadAllTextAsync(sessionFile);
                                    var session = JsonSerializer.Deserialize<Session>(json,
                                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                    if (session is not null)
                                        liveCtx.UpdateTarget(CreateStatusTable(session));
                                }
                                catch { }
                            }
                            await Task.Delay(1000);
                        }

                        result = await runTask;
                    });

                Console.WriteLine(result.Success
                    ? $"Session {result.SessionId} completed."
                    : $"Session {result.SessionId} failed: {result.ErrorMessage}");
            }

            return 0;
        });

        return command;
    }

    private string? FindSessionFile(string workflowName)
    {
        var sessionsDir = _fileSystem.Combine(_fileSystem.GetCurrentDirectory(), "sessions");
        if (!_fileSystem.DirectoryExists(sessionsDir))
            return null;

        var newest = _fileSystem.GetDirectories(sessionsDir)
            .Select(d => new { Dir = d, File = _fileSystem.Combine(d, "session.json") })
            .Where(x => _fileSystem.FileExists(x.File))
            .OrderByDescending(x => new FileInfo(x.File).CreationTimeUtc)
            .FirstOrDefault();

        if (newest is not null)
        {
            var json = _fileSystem.ReadAllTextAsync(newest.File).GetAwaiter().GetResult();
            try
            {
                var s = JsonSerializer.Deserialize<Session>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (s?.WorkflowName == workflowName)
                    return newest.File;
            }
            catch { }
        }

        return null;
    }

    private static Table CreateStatusTable(Session session)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("Step").Centered())
            .AddColumn(new TableColumn("Status").Centered())
            .AddColumn(new TableColumn("Tokens").RightAligned())
            .AddColumn(new TableColumn("Cost").RightAligned());

        foreach (var h in session.History)
        {
            var icon = h.Status switch
            {
                SessionStatus.Complete => "[green]✓[/]",
                SessionStatus.Failed => "[red]✗[/]",
                _ => " ",
            };
            table.AddRow(h.StepName, icon, "-", "-");
        }

        if (session.Budget.Count > 0)
        {
            table.AddRow(
                $"[bold]Total: {session.Status}[/]",
                $"{session.History.Count} steps",
                $"{session.TotalInputTokens:N0} in / {session.TotalOutputTokens:N0} out",
                $"[yellow]${session.TotalCostUsd:F4}[/]");
        }
        else
        {
            table.AddRow(
                $"[bold]Total: {session.Status}[/]",
                $"{session.History.Count} steps",
                "-",
                "-");
        }

        return table;
    }

    private static Table CreateStatusTable(string workflowName, string status)
    {
        return new Table()
            .Border(TableBorder.Rounded)
            .AddColumn($"[bold]{workflowName}[/] — {status}");
    }
}
