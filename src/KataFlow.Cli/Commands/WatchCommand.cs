using System.CommandLine;
using KataFlow.Core.Abstractions;

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
        var command = new Command("watch", "Tail orchestration log for a running session");

        var sessionOption = new Option<string>("--session")
        {
            Description = "Session ID to watch"
        };
        command.Add(sessionOption);

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            var sessionId = parseResult.GetRequiredValue(sessionOption);

            var logDir = _fileSystem.Combine(
                _fileSystem.GetCurrentDirectory(), "sessions", sessionId);
            if (!_fileSystem.DirectoryExists(logDir))
            {
                Console.Error.WriteLine($"Session directory not found: {logDir}");
                return 1;
            }

            var logFiles = _fileSystem.GetFiles(logDir, "orchestration-*.log");
            var logFile = logFiles.FirstOrDefault();
            if (logFile is null)
            {
                Console.WriteLine("No orchestration log file found yet. Waiting...");
                while (!ct.IsCancellationRequested)
                {
                    logFiles = _fileSystem.GetFiles(logDir, "orchestration-*.log");
                    logFile = logFiles.FirstOrDefault();
                    if (logFile is not null) break;
                    await Task.Delay(1000, ct);
                }
            }

            if (logFile is null) return 0;
            Console.WriteLine($"Tailing: {logFile}");

            using var fs = _fileSystem.OpenReadWrite(logFile);
            using var reader = new StreamReader(fs);
            fs.Seek(0, SeekOrigin.End);

            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is not null)
                    Console.WriteLine(line);
                else
                    await Task.Delay(500, ct);
            }

            return 0;
        });

        return command;
    }
}
