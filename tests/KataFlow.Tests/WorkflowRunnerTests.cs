using KataFlow.Core.Abstractions;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using KataFlow.Engine;
using KataFlow.Engine.Gates;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace KataFlow.Tests;

public class WorkflowRunnerTests
{
    private static WorkflowRunner CreateRunner(
        ISessionStore? store = null,
        StepExecutor? executor = null,
        IEnumerable<IApprovalGate>? gates = null)
    {
        store ??= CreateStore(new Session { Id = "sess-1", WorkflowName = "test-wf", Mode = OrchestratorMode.Dev });
        executor ??= Substitute.For<StepExecutor>(
            Substitute.For<ContextBuilder>(Substitute.For<IFileSystem>()),
            Substitute.For<IPromptRenderer>(),
            Substitute.For<IArtifactStore>(),
            Substitute.For<ILogger<StepExecutor>>());
        gates ??= [new AutoApprovalGate(Substitute.For<ILogger<AutoApprovalGate>>())];

        var sessionManager = new SessionManager(store);
        var logger = Substitute.For<ILogger<WorkflowRunner>>();
        var adapter = Substitute.For<IAgentAdapter>();
        adapter.Name.Returns("Test");
        adapter.AgentType.Returns(AgentType.Rest);
        adapter.SupportedChannels.Returns([ChannelType.ApiDirect, ChannelType.FileDrop]);

        return new WorkflowRunner(sessionManager, executor, logger, _ => adapter, gates);
    }

    private static ISessionStore CreateStore(Session session)
    {
        var store = Substitute.For<ISessionStore>();
        store.CreateAsync(session.WorkflowName, session.Mode).Returns(session);
        store.GetAsync(session.Id).Returns(session);
        return store;
    }

    private static WorkflowDefinition SimpleWorkflow(params StepDefinition[] steps) => new()
    {
        Name = "test-wf",
        Steps = steps.ToList().AsReadOnly(),
    };

    [Fact]
    public async Task RunAsync_CompletesFullWorkflow()
    {
        var executor = Substitute.For<StepExecutor>(
            Substitute.For<ContextBuilder>(Substitute.For<IFileSystem>()),
            Substitute.For<IPromptRenderer>(),
            Substitute.For<IArtifactStore>(),
            Substitute.For<ILogger<StepExecutor>>());
        executor.ExecuteAsync(
                Arg.Any<Session>(),
                Arg.Any<StepDefinition>(),
                Arg.Any<Func<AgentType, IAgentAdapter>>(),
                default)
            .Returns(new StepResult { StepName = "step1", Success = true, ArtifactContent = "ok" });

        var runner = CreateRunner(executor: executor);
        var workflow = SimpleWorkflow(new StepDefinition
        {
            Name = "step1", Agent = AgentType.Rest, Role = "executor", PromptTemplate = "t.md",
        });

        var result = await runner.RunAsync(workflow, new SessionContext { AutoApprove = true });

        Assert.True(result.Success);
    }

    [Fact]
    public async Task RunAsync_FailsOnStepFailure()
    {
        var executor = Substitute.For<StepExecutor>(
            Substitute.For<ContextBuilder>(Substitute.For<IFileSystem>()),
            Substitute.For<IPromptRenderer>(),
            Substitute.For<IArtifactStore>(),
            Substitute.For<ILogger<StepExecutor>>());
        executor.ExecuteAsync(
                Arg.Any<Session>(),
                Arg.Any<StepDefinition>(),
                Arg.Any<Func<AgentType, IAgentAdapter>>(),
                default)
            .Returns(new StepResult { StepName = "step1", Success = false, ErrorMessage = "boom" });

        var runner = CreateRunner(executor: executor);
        var workflow = SimpleWorkflow(new StepDefinition
        {
            Name = "step1", Agent = AgentType.Rest, Role = "executor", PromptTemplate = "t.md",
        });

        var result = await runner.RunAsync(workflow, new SessionContext { AutoApprove = true });

        Assert.False(result.Success);
        Assert.Equal("boom", result.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_SkipsParallelSteps()
    {
        var executor = Substitute.For<StepExecutor>(
            Substitute.For<ContextBuilder>(Substitute.For<IFileSystem>()),
            Substitute.For<IPromptRenderer>(),
            Substitute.For<IArtifactStore>(),
            Substitute.For<ILogger<StepExecutor>>());

        var runner = CreateRunner(executor: executor);
        var workflow = SimpleWorkflow(new StepDefinition
        {
            Name = "parallel-step", Agent = AgentType.Rest, Role = "executor",
            PromptTemplate = "t.md", Type = StepType.Parallel,
        });

        var result = await runner.RunAsync(workflow, new SessionContext { AutoApprove = true });

        Assert.True(result.Success);
        await executor.DidNotReceiveWithAnyArgs()
            .ExecuteAsync(default!, default!, default!, default);
    }
}
