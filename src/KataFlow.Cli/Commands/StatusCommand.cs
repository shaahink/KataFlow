using System.CommandLine;
using System.Text.Json;
using KataFlow.Core.Interfaces;
using Spectre.Console;

namespace KataFlow.Cli.Commands;

public class StatusCommand
{
    private readonly ISessionStore _store;

    public StatusCommand(ISessionStore store)
    {
        _store = store;
    }

    public Command Create()
    {
        var command = new Command("status", "Show session status");

        var sessionOption = new Option<string>("--session")
        {
            Description = "Show status of a specific session"
        };
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Output as JSON"
        };
        command.Add(sessionOption);
        command.Add(jsonOption);

        command.SetAction(async (ParseResult parseResult) =>
        {
            var sessionId = parseResult.GetValue(sessionOption);
            var json = parseResult.GetValue(jsonOption);

            if (!string.IsNullOrEmpty(sessionId))
            {
                var session = await _store.GetAsync(sessionId);
                if (session is null)
                {
                    Console.Error.WriteLine($"Session not found: {sessionId}");
                    return 1;
                }

                if (json)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new
                    {
                        session.Id,
                        session.WorkflowName,
                        Status = session.Status.ToString(),
                        Mode = session.Mode.ToString(),
                        session.CurrentStepIndex,
                        CreatedAt = session.CreatedAt,
                        CompletedAt = session.CompletedAt,
                        Steps = session.History.Select(s => new
                        {
                            s.StepName,
                            Status = s.Status.ToString(),
                            s.OutputArtifactPath,
                            s.ErrorMessage,
                        }),
                    }, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                    return 0;
                }

                var panel = new Panel(
                    $"[bold]Session:[/] {session.Id}\n" +
                    $"[bold]Workflow:[/] {session.WorkflowName}\n" +
                    $"[bold]Status:[/] {session.Status}\n" +
                    $"[bold]Mode:[/] {session.Mode}\n" +
                    $"[bold]Current Step:[/] {session.CurrentStepIndex}\n" +
                    $"[bold]Created:[/] {session.CreatedAt:O}\n" +
                    $"[bold]History:[/] {session.History.Count} steps\n" +
                    $"[bold]Budget:[/] ${session.TotalCostUsd:F4} ({session.TotalInputTokens} in / {session.TotalOutputTokens} out)")
                {
                    Header = new PanelHeader("Session Details"),
                    Border = BoxBorder.Rounded,
                };
                AnsiConsole.Write(panel);

                var table = new Table();
                table.AddColumn("Step");
                table.AddColumn("Status");
                table.AddColumn("Artifact");
                table.AddColumn("Error");

                foreach (var step in session.History)
                    table.AddRow(step.StepName, step.Status.ToString(), step.OutputArtifactPath ?? "-", step.ErrorMessage ?? "-");
                AnsiConsole.Write(table);
            }
            else
            {
                var sessions = await _store.ListAsync();
                if (sessions.Count == 0)
                {
                    if (json)
                        Console.WriteLine("[]");
                    else
                        Console.WriteLine("No sessions found.");
                    return 0;
                }

                if (json)
                {
                    Console.WriteLine(JsonSerializer.Serialize(sessions.Select(s => new
                    {
                        s.Id,
                        s.WorkflowName,
                        Status = s.Status.ToString(),
                        s.CurrentStepIndex,
                        CreatedAt = s.CreatedAt,
                    }), new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                    return 0;
                }

                var table = new Table();
                table.AddColumn("Session ID");
                table.AddColumn("Workflow");
                table.AddColumn("Status");
                table.AddColumn("Steps");
                table.AddColumn("Created");

                foreach (var s in sessions.OrderByDescending(s => s.CreatedAt))
                    table.AddRow(s.Id, s.WorkflowName, s.Status.ToString(), $"{s.History.Count}", s.CreatedAt.ToString("g"));
                AnsiConsole.Write(table);
            }

            return 0;
        });

        return command;
    }
}
