using Serilog;

namespace KataFlow.Infrastructure.Logging;

public static class KataFlowLogger
{
    public static Serilog.Core.Logger CreateLogger(string sessionId = "")
    {
        var config = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: string.IsNullOrEmpty(sessionId)
                    ? "logs/kataflow-.log"
                    : $"sessions/{sessionId}/orchestration-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");

        return config.CreateLogger();
    }
}
