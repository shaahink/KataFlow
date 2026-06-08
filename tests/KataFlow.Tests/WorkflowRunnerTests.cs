using KataFlow.Core.Abstractions;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using KataFlow.Engine;
using KataFlow.Engine.Gates;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace KataFlow.Tests;

[Trait("Category", "Unit")]
public class WorkflowRunnerTests
{
    private static WorkflowRunner CreateRunner(
        ISessionStore? store = null,
        Session? sessionOverride = null,
        StepExecutor? executor = null,
        IEnumerable<IApprovalGate>? gates = null)
    {
        var session = sessionOverride ?? new Session { Id = "sess-1", WorkflowName = "test-wf", Mode = OrchestratorMode.Dev };
        store ??= CreateStore(session);
        executor ??= CreateMockExecutor();
        gates ??= [new AutoApprovalGate(Substitute.For<ILogger<AutoApprovalGate>>())];

        var sessionManager = new SessionManager(store);
        var logger = Substitute.For<ILogger<WorkflowRunner>>();
        var adapter = Substitute.For<IAgentAdapter>();
        adapter.Name.Returns("Test");
        adapter.AgentType.Returns(AgentType.Rest);
        adapter.SupportedChannels.Returns([ChannelType.ApiDirect, ChannelType.FileDrop]);

        return new WorkflowRunner(sessionManager, executor, logger, _ => adapter, gates);
    }

    private static StepExecutor CreateMockExecutor(bool success = true, string content = "ok")
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
            .Returns(new StepResult { StepName = "step1", Success = success, ArtifactContent = content, ErrorMessage = success ? null : "boom" });
        return executor;
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
        var executor = CreateMockExecutor();
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
        var executor = CreateMockExecutor(success: false);
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
        var executor = CreateMockExecutor();
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

    [Fact]
    public async Task RunAsync_WaitingApproval_SkipsStepReExecution()
    {
        var executor = CreateMockExecutor();
        var session = new Session
        {
            Id = "sess-1", WorkflowName = "test-wf", Mode = OrchestratorMode.Dev,
            Status = SessionStatus.WaitingApproval,
            CurrentStepIndex = 0,
        };
        session.History.Add(new SessionStep
        {
            StepName = "step1",
            Status = SessionStatus.Complete,
            OutputArtifactPath = null,
            CompletedAt = DateTimeOffset.UtcNow,
        });

        var runner = CreateRunner(executor: executor, sessionOverride: session);
        var workflow = SimpleWorkflow(new StepDefinition
        {
            Name = "step1", Agent = AgentType.Rest, Role = "executor",
            PromptTemplate = "t.md", Approval = ApprovalMode.Manual,
        });

        var result = await runner.RunAsync(workflow, new SessionContext { AutoApprove = true });

        Assert.True(result.Success);
        await executor.DidNotReceiveWithAnyArgs()
            .ExecuteAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task RunAsync_SetsWaitingApproval_BeforeManualGate()
    {
        var executor = CreateMockExecutor();
        var session = new Session { Id = "sess-1", WorkflowName = "test-wf", Mode = OrchestratorMode.Dev };
        var store = CreateStore(session);

        var manualGate = Substitute.For<IApprovalGate>();
        manualGate.Mode.Returns(ApprovalMode.Manual);
        manualGate.RequestApprovalAsync(Arg.Any<StepResult>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var s = await store.GetAsync("sess-1");
                Assert.Equal(SessionStatus.WaitingApproval, s!.Status);
                return ApprovalDecision.Approve;
            });

        var runner = CreateRunner(store: store, sessionOverride: session, executor: executor, gates: [manualGate]);
        var workflow = SimpleWorkflow(new StepDefinition
        {
            Name = "step1", Agent = AgentType.Rest, Role = "executor",
            PromptTemplate = "t.md", Approval = ApprovalMode.Manual,
        });

        var result = await runner.RunAsync(workflow, new SessionContext());

        Assert.True(result.Success);
        await manualGate.Received(1).RequestApprovalAsync(Arg.Any<StepResult>(), Arg.Any<CancellationToken>());
    }
}
