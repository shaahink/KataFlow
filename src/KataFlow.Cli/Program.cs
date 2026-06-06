using System.CommandLine;
using KataFlow.Adapters.Claude;
using KataFlow.Adapters.FileDrop;
using KataFlow.Adapters.Rest;
using KataFlow.Cli.Commands;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Engine;
using KataFlow.Engine.Gates;
using KataFlow.Engine.Loaders;
using KataFlow.Infrastructure;
using KataFlow.Infrastructure.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

DotNetEnv.Env.Load();

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
builder.Configuration.AddJsonFile(
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kataflow", "config.json"),
    optional: true,
    reloadOnChange: false);

ConfigureServices(builder.Services);

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(KataFlowLogger.CreateLogger(), dispose: true);

var app = builder.Build();

var rootCommand = new RootCommand("KataFlow — multi-agent AI workflow orchestrator");
rootCommand.Add(app.Services.GetRequiredService<RunCommand>().Create());
rootCommand.Add(new ApproveCommand().Create());
rootCommand.Add(app.Services.GetRequiredService<StatusCommand>().Create());
rootCommand.Add(app.Services.GetRequiredService<ListCommand>().Create());
rootCommand.Add(new WatchCommand().Create());

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync(parseResult.InvocationConfiguration);

static void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IPromptRenderer, PromptRenderer>();
    services.AddSingleton<ContextBuilder>();
    services.AddSingleton<StepExecutor>();

    services.AddSingleton<PresetWorkflowRegistry>();
    services.AddSingleton<IWorkflowLoader>(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var loaders = new List<IWorkflowLoader>
        {
            sp.GetRequiredService<PresetWorkflowRegistry>(),
            new YamlWorkflowLoader(config.GetSection("KataFlow:WorkflowsPath").Value ?? "./workflows"),
        };
        return new CompositeWorkflowLoader(loaders);
    });

    services.AddSingleton<ISessionStore>(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        return new SessionStore(config.GetSection("KataFlow:SessionsPath").Value ?? "./sessions");
    });
    services.AddSingleton<IArtifactStore>(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        return new ArtifactStore(config.GetSection("KataFlow:SessionsPath").Value ?? "./sessions");
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
            config.GetSection("KataFlow:TemplatesPath").Value ?? "./templates",
            15, 500);
    });

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
    services.AddTransient<StatusCommand>();
    services.AddTransient<ListCommand>();
}
