using System.CommandLine;
using KataFlow.Cli.Commands;
using KataFlow.Infrastructure.Logging;
using KataFlow.ServiceDefaults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Serilog;

DotNetEnv.Env.Load();

Log.Logger = KataFlowLogger.CreateLogger();
try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
    builder.Configuration.AddJsonFile(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kataflow", "config.json"),
        optional: true,
        reloadOnChange: false);

    builder.Services.AddKataFlowAll(builder.Configuration);
    builder.Services.AddKataFlowGates();

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(dispose: true);

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracer => tracer
            .AddSource("KataFlow")
            .AddConsoleExporter());

    builder.Services.AddTransient<RunCommand>();
    builder.Services.AddTransient<ApproveCommand>();
    builder.Services.AddTransient<StatusCommand>();
    builder.Services.AddTransient<ListCommand>();
    builder.Services.AddTransient<WatchCommand>();

    var app = builder.Build();

    var rootCommand = new RootCommand("KataFlow — multi-agent AI workflow orchestrator");
    rootCommand.Add(app.Services.GetRequiredService<RunCommand>().Create());
    rootCommand.Add(app.Services.GetRequiredService<ApproveCommand>().Create());
    rootCommand.Add(app.Services.GetRequiredService<StatusCommand>().Create());
    rootCommand.Add(app.Services.GetRequiredService<ListCommand>().Create());
    rootCommand.Add(app.Services.GetRequiredService<WatchCommand>().Create());

    var parseResult = rootCommand.Parse(args);
    return await parseResult.InvokeAsync(parseResult.InvocationConfiguration);
}
catch (Exception ex)
{
    Log.Fatal(ex, "KataFlow terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
