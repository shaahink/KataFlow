using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using KataFlow.Engine;
using NSubstitute;

namespace KataFlow.Tests;

public class SessionManagerTests
{
    [Fact]
    public async Task ResolveAsync_CreatesNewSession()
    {
        var store = Substitute.For<ISessionStore>();
        store.CreateAsync("test-wf", OrchestratorMode.Dev)
            .Returns(new Session { Id = "new-sess", WorkflowName = "test-wf", Mode = OrchestratorMode.Dev });

        var manager = new SessionManager(store);
        var workflow = new WorkflowDefinition
        {
            Name = "test-wf",
            Steps = new List<StepDefinition>().AsReadOnly(),
        };

        var session = await manager.ResolveAsync(workflow, new SessionContext());

        Assert.Equal("new-sess", session.Id);
        await store.Received(1).SaveAsync(session);
    }

    [Fact]
    public async Task ResolveAsync_ResumesExistingSession()
    {
        var store = Substitute.For<ISessionStore>();
        store.GetAsync("sess-1").Returns(new Session
        {
            Id = "sess-1",
            WorkflowName = "test-wf",
            Mode = OrchestratorMode.Dev,
            Status = SessionStatus.WaitingApproval,
        });

        var manager = new SessionManager(store);
        var workflow = new WorkflowDefinition
        {
            Name = "test-wf",
            Steps = new List<StepDefinition>().AsReadOnly(),
        };

        var session = await manager.ResolveAsync(workflow, new SessionContext
        {
            SessionId = "sess-1",
            Variables = new Dictionary<string, string> { ["x"] = "y" },
        });

        Assert.Equal("sess-1", session.Id);
        Assert.Equal("y", session.Variables["x"]);
    }

    [Fact]
    public async Task FailAsync_SetsFailedStatus()
    {
        var store = Substitute.For<ISessionStore>();
        var manager = new SessionManager(store);

        var session = new Session { Id = "sess-1", WorkflowName = "wf", Mode = OrchestratorMode.Dev };
        var result = await manager.FailAsync(session, "Something went wrong");

        Assert.False(result.Success);
        Assert.Equal("Something went wrong", result.ErrorMessage);
        Assert.Equal(SessionStatus.Failed, session.Status);
        await store.Received(1).SaveAsync(session);
    }
}
