using System.CommandLine;
using System.Text.Json;
using KataFlow.Core.Abstractions;
using KataFlow.Core.Enums;
using KataFlow.Core.Models;

namespace KataFlow.Cli.Commands;

public class WatchCommand
{
    private readonly IFileSystem _fileSystem;

    public WatchCommand(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public Command Create()
    {
        var command = new Command("watch", "Watch a running session for status updates");

        var sessionOption = new Option<string>("--session")
        {
            Description = "Session ID to watch"
        };
        command.Add(sessionOption);

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            var sessionId = parseResult.GetRequiredValue(sessionOption);
            var sessionFile = _fileSystem.Combine(
                _fileSystem.GetCurrentDirectory(), "sessions", sessionId, "session.json");

            if (!_fileSystem.FileExists(sessionFile))
            {
                Console.Error.WriteLine($"Session not found: {sessionId}");
                return 1;
            }

            Console.WriteLine($"Watching session: {sessionId}");
            Console.WriteLine("Press Ctrl+C to stop.\n");

            var lastStatus = "";
            var lastStepIndex = -1;
            var lastBudget = "";

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var json = await _fileSystem.ReadAllTextAsync(sessionFile, ct);
                    var session = JsonSerializer.Deserialize<Session>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (session is null) { await Task.Delay(1000, ct); continue; }

                    var statusLine = $"[{DateTimeOffset.Now:HH:mm:ss}] Status: {session.Status}  Step: {session.CurrentStepIndex}";
                    var budgetLine = session.TotalCostUsd > 0
                        ? $"  Cost: ${session.TotalCostUsd:F4}  Tokens: {session.TotalInputTokens}in/{session.TotalOutputTokens}out"
                        : "";

                    if (session.Status.ToString() != lastStatus
                        || session.CurrentStepIndex != lastStepIndex
                        || budgetLine != lastBudget)
                    {
                        Console.WriteLine(statusLine + budgetLine);

                        var latest = session.History.LastOrDefault();
                        if (latest is not null)
                            Console.WriteLine($"  Last step: {latest.StepName} → {latest.Status}");

                        lastStatus = session.Status.ToString();
                        lastStepIndex = session.CurrentStepIndex;
                        lastBudget = budgetLine;
                    }

                    if (session.Status is SessionStatus.Complete or SessionStatus.Failed or SessionStatus.Cancelled)
                    {
                        Console.WriteLine($"\nSession finished: {session.Status}");
                        if (session.TotalCostUsd > 0)
                            Console.WriteLine($"Total cost: ${session.TotalCostUsd:F4}");
                        break;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // session file may be mid-write; ignore and retry
                }

                await Task.Delay(1500, ct);
            }

            return 0;
        });

        return command;
    }
}
