using System.CommandLine;
using KataFlow.Core.Interfaces;
using Spectre.Console;

namespace KataFlow.Cli.Commands;

public static class StatusCommand
{
    public static Command Create()
    {
        var command = new Command("status", "Show session status");

        var sessionOption = new Option<string>("--session")
        {
            Description = "Show status of a specific session"
        };
        command.Add(sessionOption);

        command.SetAction(async (ParseResult parseResult) =>
        {
            var sessionId = parseResult.GetValue(sessionOption);
            var store = ServiceProviderInstance.GetService<ISessionStore>();

            if (!string.IsNullOrEmpty(sessionId))
            {
                var session = await store.GetAsync(sessionId);
                if (session is null)
                {
                    Console.Error.WriteLine($"Session not found: {sessionId}");
                    return 1;
                }

                var panel = new Panel(
                    $"[bold]Session:[/] {session.Id}\n" +
                    $"[bold]Workflow:[/] {session.WorkflowName}\n" +
                    $"[bold]Status:[/] {session.Status}\n" +
                    $"[bold]Mode:[/] {session.Mode}\n" +
                    $"[bold]Current Step:[/] {session.CurrentStepIndex}\n" +
                    $"[bold]Created:[/] {session.CreatedAt:O}\n" +
                    $"[bold]History:[/] {session.History.Count} steps")
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
                var sessions = await store.ListAsync();
                if (sessions.Count == 0)
                {
                    Console.WriteLine("No sessions found.");
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
