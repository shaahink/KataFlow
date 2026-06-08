using KataFlow.Core;
using KataFlow.Adapters.Claude;
using KataFlow.Adapters.CliExecute;
using KataFlow.Adapters.FileDrop;
using KataFlow.Adapters.Rest;
using KataFlow.Core.Abstractions;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Engine;
using KataFlow.Engine.Gates;
using KataFlow.Engine.Loaders;
using KataFlow.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KataFlow.ServiceDefaults;

public static class KataFlowServiceExtensions
{
    public static IServiceCollection AddKataFlowCore(this IServiceCollection services)
    {
        services.AddSingleton<IFileSystem, SystemFileSystem>();
        services.AddSingleton<IPromptRenderer, PromptRenderer>();
        services.AddSingleton<ContextBuilder>();
        services.AddSingleton<StepExecutor>();
        services.AddSingleton<SessionManager>();
        services.AddSingleton<ApprovalFileSignal>();
        services.AddSingleton<PresetWorkflowRegistry>();
        return services;
    }

    public static IServiceCollection AddKataFlowPersistence(this IServiceCollection services, IConfiguration? config = null)
    {
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
        return services;
    }

    public static IServiceCollection AddKataFlowWorkflowLoader(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IWorkflowLoader>(sp =>
        {
            var fs = sp.GetRequiredService<IFileSystem>();
            var loaders = new List<IWorkflowLoader>
            {
                sp.GetRequiredService<PresetWorkflowRegistry>(),
                new YamlWorkflowLoader(fs,
                    config.GetSection(Constants.ConfigKeyWorkflowsPath).Value ?? Constants.WorkflowsDefaultPath),
            };
            return new CompositeWorkflowLoader(loaders);
        });
        return services;
    }

    public static IServiceCollection AddKataFlowChannels(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<FileWatcher>();
        services.AddSingleton<FileDropChannel>(sp =>
        {
            return new FileDropChannel(
                sp.GetRequiredService<ILogger<FileDropChannel>>(),
                sp.GetRequiredService<FileWatcher>(),
                sp.GetRequiredService<IPromptRenderer>(),
                sp.GetRequiredService<IFileSystem>(),
                config.GetSection(Constants.ConfigKeyTemplatesPath).Value ?? Constants.TemplatesDefaultPath,
                15, 500);
        });
        services.AddOptions<CliExecuteOptions>().BindConfiguration(Constants.ConfigSectionCliExecute);
        services.AddSingleton<CliExecuteChannel>();
        return services;
    }

    public static IServiceCollection AddKataFlowClaude(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<ClaudeOptions>()
            .BindConfiguration(Constants.ConfigSectionClaude)
            .PostConfigure(o =>
            {
                o.ApiKey = Environment.GetEnvironmentVariable(Constants.ClaudeApiKeyEnv) ?? o.ApiKey;
            });
        services.AddHttpClient<ClaudeApiChannel>(client =>
        {
            client.BaseAddress = new Uri(Constants.ClaudeApiUrl);
        });
        services.AddSingleton<IAgentAdapter>(sp =>
        {
            return new ClaudeAdapter([
                sp.GetRequiredService<FileDropChannel>(),
                sp.GetRequiredService<CliExecuteChannel>(),
                sp.GetRequiredService<ClaudeApiChannel>(),
            ]);
        });
        return services;
    }

    public static IServiceCollection AddKataFlowRest(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<RestOptions>()
            .BindConfiguration(Constants.ConfigSectionRest)
            .PostConfigure(o =>
            {
                o.ApiKey = Environment.GetEnvironmentVariable(Constants.DeepSeekApiKeyEnv) ?? o.ApiKey;
            });
        services.AddHttpClient<RestApiChannel>();
        services.AddSingleton<IAgentAdapter>(sp =>
        {
            return new RestAdapter([
                sp.GetRequiredService<FileDropChannel>(),
                sp.GetRequiredService<CliExecuteChannel>(),
                sp.GetRequiredService<RestApiChannel>(),
            ]);
        });
        return services;
    }

    public static IServiceCollection AddKataFlowRunner(this IServiceCollection services)
    {
        services.AddSingleton<Func<AgentType, IAgentAdapter>>(sp => agentType => agentType switch
        {
            AgentType.Claude => sp.GetServices<IAgentAdapter>().First(a => a is ClaudeAdapter),
            AgentType.Rest   => sp.GetServices<IAgentAdapter>().First(a => a is RestAdapter),
            AgentType.Script => throw new InvalidOperationException("Script steps do not use an adapter"),
            _ => throw new InvalidOperationException($"Unknown agent type: {agentType}"),
        });
        services.AddSingleton<IWorkflowRunner, WorkflowRunner>();
        return services;
    }

    public static IServiceCollection AddKataFlowGates(this IServiceCollection services)
    {
        services.AddSingleton<IApprovalGate, AutoApprovalGate>();
        services.AddSingleton<IApprovalGate, ManualApprovalGate>();
        return services;
    }

    public static IServiceCollection AddKataFlowAll(this IServiceCollection services, IConfiguration config)
    {
        services.AddKataFlowCore();
        services.AddKataFlowPersistence(config);
        services.AddKataFlowWorkflowLoader(config);
        services.AddKataFlowChannels(config);
        services.AddKataFlowClaude(config);
        services.AddKataFlowRest(config);
        services.AddKataFlowRunner();
        return services;
    }
}
