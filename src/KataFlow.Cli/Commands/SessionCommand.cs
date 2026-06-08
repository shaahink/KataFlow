using System.CommandLine;
using KataFlow.Core.Abstractions;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using Spectre.Console;

namespace KataFlow.Cli.Commands;

public class SessionCommand
{
    private readonly ISessionStore _store;
    private readonly IFileSystem _fs;

    public SessionCommand(ISessionStore store, IFileSystem fs)
    {
        _store = store;
        _fs = fs;
    }

    public Command Create()
    {
        var command = new Command("session", "Manage sessions (delete, clean)");

        var deleteCommand = new Command("delete", "Delete a session");
        var deleteSessionOption = new Option<string>("--session")
        {
            Description = "Session ID to delete"
        };
        deleteCommand.Add(deleteSessionOption);
        deleteCommand.SetAction(async (ParseResult parseResult) =>
        {
            var sessionId = parseResult.GetRequiredValue(deleteSessionOption);
            var session = await _store.GetAsync(sessionId);
            if (session is null)
            {
                Console.Error.WriteLine($"Session not found: {sessionId}");
                return 1;
            }

            var sessionDir = _fs.Combine(_fs.GetCurrentDirectory(), "sessions", sessionId);
            if (_fs.DirectoryExists(sessionDir))
            {
                Directory.Delete(sessionDir, recursive: true);
                AnsiConsole.MarkupLine($"[green]Deleted session:[/] {sessionId}");
            }
            else
            {
                Console.Error.WriteLine($"Session directory not found: {sessionDir}");
                return 1;
            }

            return 0;
        });

        var cleanCommand = new Command("clean", "Delete all completed, failed, and cancelled sessions");
        cleanCommand.SetAction(async (ParseResult parseResult) =>
        {
            var sessions = await _store.ListAsync();
            var toDelete = sessions.Where(s => s.Status is SessionStatus.Complete
                or SessionStatus.Failed
                or SessionStatus.Cancelled).ToList();

            if (toDelete.Count == 0)
            {
                AnsiConsole.MarkupLine("[dim]No completed, failed, or cancelled sessions to clean.[/]");
                return 0;
            }

            var confirm = AnsiConsole.Confirm(
                $"Delete {toDelete.Count} session(s)? This cannot be undone.", false);
            if (!confirm)
            {
                AnsiConsole.MarkupLine("[dim]Clean cancelled.[/]");
                return 0;
            }

            foreach (var s in toDelete)
            {
                var dir = _fs.Combine(_fs.GetCurrentDirectory(), "sessions", s.Id);
                if (_fs.DirectoryExists(dir))
                    Directory.Delete(dir, recursive: true);
            }

            AnsiConsole.MarkupLine($"[green]Cleaned {toDelete.Count} session(s).[/]");
            return 0;
        });

        command.Add(deleteCommand);
        command.Add(cleanCommand);
        return command;
    }
}
