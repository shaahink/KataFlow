using System.CommandLine;
using System.Text.Json;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using Spectre.Console;

namespace KataFlow.Cli.Commands;

public class ListCommand
{
    private readonly IWorkflowLoader _loader;
    private readonly ISessionStore _store;

    public ListCommand(IWorkflowLoader loader, ISessionStore store)
    {
        _loader = loader;
        _store = store;
    }

    public Command Create()
    {
        var command = new Command("list", "List available workflows or sessions");

        var targetArg = new Argument<string>("target")
        {
            Description = "What to list (workflows|sessions)"
        };
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Output as JSON"
        };
        command.Add(targetArg);
        command.Add(jsonOption);

        command.SetAction(async (ParseResult parseResult) =>
        {
            var target = parseResult.GetRequiredValue(targetArg);
            var json = parseResult.GetValue(jsonOption);

            switch (target.ToLowerInvariant())
            {
                case "workflows":
                    var available = _loader.ListAvailable();
                    if (available.Count == 0)
                    {
                        if (json)
                            Console.WriteLine("[]");
                        else
                            Console.WriteLine("No workflows available.");
                        return 0;
                    }

                    if (json)
                    {
                        Console.WriteLine(JsonSerializer.Serialize(available, new JsonSerializerOptions { WriteIndented = true }));
                        return 0;
                    }

                    var table = new Table();
                    table.AddColumn("Name");
                    foreach (var w in available.Order())
                        table.AddRow(w);
                    AnsiConsole.Write(table);
                    break;

                case "sessions":
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
                            Steps = s.History.Count,
                            Cost = s.TotalCostUsd,
                        }), new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                        return 0;
                    }

                    var st = new Table()
                        .Border(TableBorder.Rounded);
                    st.AddColumn(new TableColumn("Session ID").LeftAligned());
                    st.AddColumn(new TableColumn("Workflow").LeftAligned());
                    st.AddColumn(new TableColumn("Status").Centered());
                    st.AddColumn(new TableColumn("Steps").RightAligned());
                    st.AddColumn(new TableColumn("Cost").RightAligned());
                    st.AddColumn(new TableColumn("Created").RightAligned());

                    foreach (var s in sessions.OrderByDescending(s => s.CreatedAt))
                    {
                        var idShort = s.Id.Length > 8 ? s.Id[..8] : s.Id;
                        var statusColor = s.Status switch
                        {
                            SessionStatus.Running => $"[yellow]{s.Status}[/]",
                            SessionStatus.WaitingApproval => $"[cyan]{s.Status}[/]",
                            SessionStatus.Complete => $"[green]{s.Status}[/]",
                            SessionStatus.Failed => $"[red]{s.Status}[/]",
                            SessionStatus.Cancelled => $"[dim]{s.Status}[/]",
                            _ => s.Status.ToString(),
                        };

                        var cost = s.TotalCostUsd > 0
                            ? $"[yellow]${s.TotalCostUsd:F4}[/]"
                            : "-";

                        var duration = s.CompletedAt.HasValue
                            ? (s.CompletedAt.Value - s.CreatedAt).ToString(@"h\h\ m\m")
                            : (DateTimeOffset.UtcNow - s.CreatedAt).ToString(@"h\h\ m\m");

                        st.AddRow(
                            new Markup(idShort),
                            new Markup(s.WorkflowName),
                            new Markup(statusColor),
                            new Markup($"{s.History.Count}/{s.CurrentStepIndex}"),
                            new Markup(cost),
                            new Markup(duration));
                    }
                    AnsiConsole.Write(st);
                    break;

                default:
                    Console.Error.WriteLine($"Unknown target: {target}. Use 'workflows' or 'sessions'.");
                    return 1;
            }

            return 0;
        });

        return command;
    }
}
