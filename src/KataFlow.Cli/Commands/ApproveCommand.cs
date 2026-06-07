using System.CommandLine;
using KataFlow.Core.Abstractions;
using KataFlow.Core;

namespace KataFlow.Cli.Commands;

public class ApproveCommand
{
    private readonly IFileSystem _fileSystem;

    public ApproveCommand(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public Command Create()
    {
        var command = new Command("approve", "Approve or reject a paused session");

        var sessionOption = new Option<string>("--session")
        {
            Description = "Session ID to approve"
        };
        var rejectOption = new Option<bool>("--reject")
        {
            Description = "Reject instead of approve"
        };

        command.Add(sessionOption);
        command.Add(rejectOption);

        command.SetAction((ParseResult parseResult) =>
        {
            var sessionId = parseResult.GetRequiredValue(sessionOption);
            var reject = parseResult.GetValue(rejectOption);

            var sessionsDir = _fileSystem.Combine(
                _fileSystem.GetCurrentDirectory(), "sessions", sessionId);
            if (!_fileSystem.DirectoryExists(sessionsDir))
            {
                Console.Error.WriteLine($"Session directory not found: {sessionsDir}");
                return 1;
            }

            var pendingFile = _fileSystem.Combine(sessionsDir, ".pending-approval");
            if (!_fileSystem.FileExists(pendingFile))
            {
                Console.Error.WriteLine($"No pending approval for session {sessionId}");
                return 1;
            }

            var decisionFile = reject
                ? _fileSystem.Combine(sessionsDir, Constants.RejectedFile)
                : _fileSystem.Combine(sessionsDir, Constants.ApprovedFile);

            _fileSystem.WriteAllText(decisionFile, sessionId);
            Console.WriteLine(reject
                ? $"Session {sessionId} marked for rejection."
                : $"Session {sessionId} approved.");
            return 0;
        });

        return command;
    }
}
