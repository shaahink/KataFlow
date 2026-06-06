using System.CommandLine;

namespace KataFlow.Cli.Commands;

public class ApproveCommand
{
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

            var sessionsDir = Path.Combine(Directory.GetCurrentDirectory(), "sessions", sessionId);
            if (!Directory.Exists(sessionsDir))
            {
                Console.Error.WriteLine($"Session directory not found: {sessionsDir}");
                return 1;
            }

            var pendingFile = Path.Combine(sessionsDir, ".pending-approval");
            if (!File.Exists(pendingFile))
            {
                Console.Error.WriteLine($"No pending approval for session {sessionId}");
                return 1;
            }

            var decisionFile = reject
                ? Path.Combine(sessionsDir, ".rejected")
                : Path.Combine(sessionsDir, ".approved");

            File.WriteAllText(decisionFile, sessionId);
            Console.WriteLine(reject
                ? $"Session {sessionId} marked for rejection."
                : $"Session {sessionId} approved.");
            return 0;
        });

        return command;
    }
}
