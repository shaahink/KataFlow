using KataFlow.Core.Abstractions;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using KataFlow.Engine;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace KataFlow.Tests;

public class StepExecutorTests
{
    private static ContextBuilder CreateContextBuilder()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.GetCurrentDirectory().Returns("/test");
        fs.Combine(Arg.Any<string>(), Arg.Any<string>())
            .Returns(c => Path.Combine(c.ArgAt<string>(0), c.ArgAt<string>(1)));
        fs.Combine(Arg.Any<string[]>())
            .Returns(c => Path.Combine(c.Arg<string[]>()));
        return new ContextBuilder(fs);
    }

    private static IAgentAdapter CreateAdapter(string content = "## Output", bool success = true, string? error = null)
    {
        var adapter = Substitute.For<IAgentAdapter>();
        adapter.Name.Returns("Test");
        adapter.AgentType.Returns(AgentType.Rest);
        adapter.SupportedChannels.Returns([ChannelType.ApiDirect, ChannelType.FileDrop]);
        adapter.SendAsync(Arg.Any<AgentRequest>(), Arg.Any<ChannelType>(), Arg.Any<CancellationToken>())
            .Returns(new AgentResponse { Content = content, Success = success, ErrorMessage = error });
        return adapter;
    }

    private static StepExecutor CreateExecutor(
        ContextBuilder? contextBuilder = null,
        IPromptRenderer? renderer = null,
        IArtifactStore? store = null)
    {
        return new StepExecutor(
            contextBuilder ?? CreateContextBuilder(),
            renderer ?? Substitute.For<IPromptRenderer>(),
            store ?? Substitute.For<IArtifactStore>(),
            Substitute.For<ILogger<StepExecutor>>());
    }

    [Fact]
    public async Task ExecuteAsync_Success_StoresArtifact()
    {
        var renderer = Substitute.For<IPromptRenderer>();
        renderer.Render(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns("rendered prompt");

        var store = Substitute.For<IArtifactStore>();
        store.GetPath(Arg.Any<Session>(), "result").Returns("/path/to/result.md");

        var executor = CreateExecutor(renderer: renderer, store: store);
        var adapter = CreateAdapter();

        var session = new Session { Id = "sess-1", WorkflowName = "wf", Mode = OrchestratorMode.Dev };
        var step = new StepDefinition
        {
            Name = "test-step",
            Agent = AgentType.Rest,
            Role = "executor",
            PromptTemplate = "template.md",
            OutputArtifactName = "result",
            MaxRetries = 0,
        };

        var result = await executor.ExecuteAsync(session, step, _ => adapter, default);

        Assert.True(result.Success);
        Assert.Equal("test-step", result.StepName);
        Assert.Equal("## Output", result.ArtifactContent);
        await store.Received(1).SaveAsync(session, "result", "## Output");
    }

    [Fact]
    public async Task ExecuteAsync_FailsAfterMaxRetries()
    {
        var renderer = Substitute.For<IPromptRenderer>();
        renderer.Render(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns("rendered prompt");

        var executor = CreateExecutor(renderer: renderer);
        var adapter = CreateAdapter(content: "", success: false, error: "API error");

        var session = new Session { Id = "sess-1", WorkflowName = "wf", Mode = OrchestratorMode.Dev };
        var step = new StepDefinition
        {
            Name = "failing-step",
            Agent = AgentType.Rest,
            Role = "executor",
            PromptTemplate = "template.md",
            MaxRetries = 1,
        };

        var result = await executor.ExecuteAsync(session, step, _ => adapter, default);

        Assert.False(result.Success);
        Assert.Equal("API error", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_Captured()
    {
        var renderer = Substitute.For<IPromptRenderer>();
        renderer.Render(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns("rendered prompt");

        var executor = CreateExecutor(renderer: renderer);
        var adapter = Substitute.For<IAgentAdapter>();
        adapter.Name.Returns("Test");
        adapter.AgentType.Returns(AgentType.Rest);
        adapter.SupportedChannels.Returns([ChannelType.ApiDirect, ChannelType.FileDrop]);
        adapter.SendAsync(Arg.Any<AgentRequest>(), Arg.Any<ChannelType>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.Arg<CancellationToken>();
                await Task.Delay(TimeSpan.FromDays(1), ct);
                return new AgentResponse { Content = "late", Success = true };
            });

        var session = new Session { Id = "sess-1", WorkflowName = "wf", Mode = OrchestratorMode.Dev };
        var step = new StepDefinition
        {
            Name = "timeout-step",
            Agent = AgentType.Rest,
            Role = "executor",
            PromptTemplate = "template.md",
            Timeout = TimeSpan.FromMilliseconds(1),
            MaxRetries = 0,
        };

        var result = await executor.ExecuteAsync(session, step, _ => adapter, default);

        Assert.False(result.Success);
        Assert.Contains("timed out", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
