using Serilog;

namespace KataFlow.Infrastructure.Logging;

public static class KataFlowLogger
{
    private static Serilog.Core.Logger? _logger;

    public static Serilog.Core.Logger CreateLogger()
    {
        if (_logger is not null) return _logger;

        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/kataflow-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        return _logger;
    }

    public static void CloseAndFlush()
    {
        _logger?.Dispose();
        _logger = null;
    }
}
