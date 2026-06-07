using System.CommandLine;
using KataFlow.Adapters.Claude;
using KataFlow.Adapters.CliExecute;
using KataFlow.Adapters.FileDrop;
using KataFlow.Adapters.Rest;
using KataFlow.Cli.Commands;
using KataFlow.Core.Abstractions;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Engine;
using KataFlow.Engine.Gates;
using KataFlow.Engine.Loaders;
using KataFlow.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Serilog;

DotNetEnv.Env.Load();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/kataflow-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
    builder.Configuration.AddJsonFile(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kataflow", "config.json"),
        optional: true,
        reloadOnChange: false);

    ConfigureServices(builder.Services);

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(dispose: true);

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracer => tracer
            .AddSource("KataFlow")
            .AddConsoleExporter());

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

static void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IFileSystem, SystemFileSystem>();

    services.AddSingleton<IPromptRenderer, PromptRenderer>();
    services.AddSingleton<ContextBuilder>();
    services.AddSingleton<StepExecutor>();
    services.AddSingleton<SessionManager>();
    services.AddSingleton<ApprovalFileSignal>();

    services.AddSingleton<PresetWorkflowRegistry>();
    services.AddSingleton<IWorkflowLoader>(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var fs = sp.GetRequiredService<IFileSystem>();
        var loaders = new List<IWorkflowLoader>
        {
            sp.GetRequiredService<PresetWorkflowRegistry>(),
            new YamlWorkflowLoader(fs, config.GetSection("KataFlow:WorkflowsPath").Value ?? "./workflows"),
        };
        return new CompositeWorkflowLoader(loaders);
    });

    services.AddSingleton<ISessionStore>(sp =>
    {
        var fs = sp.GetRequiredService<IFileSystem>();
        return new SessionStore(fs);
    });
    services.AddSingleton<IArtifactStore>(sp =>
    {
        var fs = sp.GetRequiredService<IFileSystem>();
        return new ArtifactStore(fs);
    });

    services.AddSingleton<IApprovalGate, AutoApprovalGate>();
    services.AddSingleton<IApprovalGate, ManualApprovalGate>();

    services.AddSingleton<FileWatcher>();

    services.AddSingleton<FileDropChannel>(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        return new FileDropChannel(
            sp.GetRequiredService<ILogger<FileDropChannel>>(),
            sp.GetRequiredService<FileWatcher>(),
            sp.GetRequiredService<IPromptRenderer>(),
            sp.GetRequiredService<IFileSystem>(),
            config.GetSection("KataFlow:TemplatesPath").Value ?? "./templates",
            15, 500);
    });

    services.AddOptions<CliExecuteOptions>().BindConfiguration("Agents:CliExecute");
    services.AddSingleton<CliExecuteChannel>();

    services.AddOptions<ClaudeOptions>()
        .BindConfiguration("Agents:Claude")
        .PostConfigure(o =>
        {
            o.ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? o.ApiKey;
        });
    services.AddHttpClient<ClaudeApiChannel>(client =>
    {
        client.BaseAddress = new Uri("https://api.anthropic.com");
    });
    services.AddSingleton<IAgentAdapter>(sp =>
    {
        var channels = new List<IAgentChannel>
        {
            sp.GetRequiredService<FileDropChannel>(),
            sp.GetRequiredService<ClaudeApiChannel>(),
        };
        return new ClaudeAdapter(channels);
    });

    services.AddOptions<RestOptions>()
        .BindConfiguration("Agents:Rest")
        .PostConfigure(o =>
        {
            o.ApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? o.ApiKey;
        });
    services.AddHttpClient<RestApiChannel>();
    services.AddSingleton<IAgentAdapter>(sp =>
    {
        var channels = new List<IAgentChannel>
        {
            sp.GetRequiredService<RestApiChannel>(),
        };
        return new RestAdapter(channels);
    });

    services.AddSingleton<Func<AgentType, IAgentAdapter>>(sp => agentType => agentType switch
    {
        AgentType.Claude => sp.GetServices<IAgentAdapter>().First(a => a is ClaudeAdapter),
        AgentType.Rest => sp.GetServices<IAgentAdapter>().First(a => a is RestAdapter),
        _ => throw new InvalidOperationException($"Unknown agent type: {agentType}")
    });
    services.AddSingleton<IWorkflowRunner, WorkflowRunner>();

    services.AddTransient<RunCommand>();
    services.AddTransient<ApproveCommand>();
    services.AddTransient<StatusCommand>();
    services.AddTransient<ListCommand>();
    services.AddTransient<WatchCommand>();
}
