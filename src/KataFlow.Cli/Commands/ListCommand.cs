using System.CommandLine;
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
        command.Add(targetArg);

        command.SetAction(async (ParseResult parseResult) =>
        {
            var target = parseResult.GetRequiredValue(targetArg);

            switch (target.ToLowerInvariant())
            {
                case "workflows":
                    var available = _loader.ListAvailable();
                    if (available.Count == 0)
                    {
                        Console.WriteLine("No workflows available.");
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
                        Console.WriteLine("No sessions found.");
                        return 0;
                    }
                    var st = new Table();
                    st.AddColumn("Session ID");
                    st.AddColumn("Workflow");
                    st.AddColumn("Status");
                    foreach (var s in sessions.OrderByDescending(s => s.CreatedAt))
                        st.AddRow(s.Id, s.WorkflowName, s.Status.ToString());
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
