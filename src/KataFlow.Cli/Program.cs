using System.CommandLine;
using System.CommandLine.Parsing;
using KataFlow.Adapters.Claude;
using Microsoft.Extensions.Configuration;
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
ServiceProviderInstance.ServiceProvider = app.Services;

var rootCommand = new RootCommand("KataFlow — multi-agent AI workflow orchestrator");
rootCommand.Add(RunCommand.Create());
rootCommand.Add(ApproveCommand.Create());
rootCommand.Add(StatusCommand.Create());
rootCommand.Add(ListCommand.Create());
rootCommand.Add(WatchCommand.Create());

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
        var presets = sp.GetRequiredService<PresetWorkflowRegistry>();
        var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        return new CompositeWorkflowLoader(presets, new YamlWorkflowLoader(
            config.GetSection("KataFlow:WorkflowsPath").Value ?? "./workflows"));
    });
    services.AddSingleton<ISessionStore>(sp =>
    {
        var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        return new SessionStore(config.GetSection("KataFlow:SessionsPath").Value ?? "./sessions");
    });
    services.AddSingleton<IArtifactStore>(sp =>
    {
        var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        return new ArtifactStore(config.GetSection("KataFlow:SessionsPath").Value ?? "./sessions");
    });
    services.AddSingleton<AutoApprovalGate>();
    services.AddSingleton<ManualApprovalGate>();
    services.AddSingleton<FileWatcher>();
    services.AddSingleton<FileDropChannel>(sp =>
    {
        var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        return new FileDropChannel(
            sp.GetRequiredService<ILogger<FileDropChannel>>(),
            sp.GetRequiredService<FileWatcher>(),
            sp.GetRequiredService<IPromptRenderer>(),
            config.GetSection("KataFlow:TemplatesPath").Value ?? "./templates",
            config.GetValue<int>("Agents:Rest:FileDrop:WatchTimeoutMinutes", 15),
            config.GetValue<int>("Agents:Rest:FileDrop:PollIntervalMs", 500));
    });
    services.AddSingleton<ClaudeApiChannel>(sp =>
    {
        var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        return new ClaudeApiChannel(
            sp.GetRequiredService<ILogger<ClaudeApiChannel>>(),
            Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? config.GetSection("Agents:Claude:ApiKey").Value ?? "",
            config.GetSection("Agents:Claude:Model").Value ?? "claude-sonnet-4-6",
            config.GetValue<int>("Agents:Claude:MaxTokens", 16384));
    });
    services.AddSingleton<ClaudeAdapter>();
    services.AddSingleton<RestApiChannel>(sp =>
    {
        var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        return new RestApiChannel(
            sp.GetRequiredService<ILogger<RestApiChannel>>(),
            new HttpClient(),
            config.GetSection("Agents:Rest:BaseUrl").Value ?? "https://api.deepseek.com",
            Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? config.GetSection("Agents:Rest:ApiKey").Value ?? "",
            config.GetSection("Agents:Rest:Model").Value ?? "deepseek-chat",
            config.GetValue<int>("Agents:Rest:MaxTokens", 16384));
    });
    services.AddSingleton<RestAdapter>();
    services.AddSingleton<Func<AgentType, IAgentAdapter>>(sp => agentType => agentType switch
    {
        AgentType.Claude => sp.GetRequiredService<ClaudeAdapter>(),
        AgentType.Rest => sp.GetRequiredService<RestAdapter>(),
        _ => throw new InvalidOperationException($"Unknown agent type: {agentType}")
    });
    services.AddSingleton<IWorkflowRunner, WorkflowRunner>();
}

public static class ServiceProviderInstance
{
    public static IServiceProvider ServiceProvider { get; set; } = null!;
    public static T GetService<T>() where T : notnull => ServiceProvider.GetRequiredService<T>();
}
