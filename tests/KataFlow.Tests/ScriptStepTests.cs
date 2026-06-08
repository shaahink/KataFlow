using KataFlow.Core.Abstractions;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using KataFlow.Engine;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace KataFlow.Tests;

[Trait("Category", "Unit")]
public class ScriptStepTests
{
    private static ContextBuilder CreateContextBuilder()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.GetCurrentDirectory().Returns(Environment.CurrentDirectory);
        fs.Combine(Arg.Any<string>(), Arg.Any<string>())
            .Returns(c => Path.Combine(c.ArgAt<string>(0), c.ArgAt<string>(1)));
        fs.Combine(Arg.Any<string[]>())
            .Returns(c => Path.Combine(c.Arg<string[]>()));
        return new ContextBuilder(fs);
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
    public async Task ExecuteScriptStep_Success_CapturesStdout()
    {
        var renderer = Substitute.For<IPromptRenderer>();
        renderer.Render(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns("cmd /c echo SCRIPT_OK");

        var executor = CreateExecutor(renderer: renderer);
        var session = new Session { Id = "sess-1", WorkflowName = "wf", Mode = OrchestratorMode.Dev };
        var step = new StepDefinition
        {
            Name = "test-script",
            Agent = AgentType.Script,
            Role = "tester",
            PromptTemplate = "",
            ScriptCommand = "cmd /c echo SCRIPT_OK",
        };

        var result = await executor.ExecuteAsync(session, step, _ => throw new("unused"), default);

        Assert.True(result.Success);
        Assert.Equal("test-script", result.StepName);
        Assert.Contains("SCRIPT_OK", result.ArtifactContent);
        Assert.Single(session.History);
        Assert.Equal(SessionStatus.Complete, session.History[0].Status);
    }

    [Fact]
    public async Task ExecuteScriptStep_NonZeroExit_ReturnsFailure()
    {
        var renderer = Substitute.For<IPromptRenderer>();
        renderer.Render(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns("cmd /c exit 1");

        var executor = CreateExecutor(renderer: renderer);
        var session = new Session { Id = "sess-1", WorkflowName = "wf", Mode = OrchestratorMode.Dev };
        var step = new StepDefinition
        {
            Name = "failing-script",
            Agent = AgentType.Script,
            Role = "tester",
            PromptTemplate = "",
            ScriptCommand = "cmd /c exit 1",
        };

        var result = await executor.ExecuteAsync(session, step, _ => throw new("unused"), default);

        Assert.False(result.Success);
        Assert.Contains("Script exited with code 1", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteScriptStep_RendersCommandVariables()
    {
        var renderer = Substitute.For<IPromptRenderer>();
        renderer.Render(Arg.Is<string>(s => s.Contains("{{project}}")), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns("cmd /c echo MyProject_RENDERED");

        var store = Substitute.For<IArtifactStore>();
        store.GetPath(Arg.Any<Session>(), "output").Returns("/tmp/output.md");

        var executor = CreateExecutor(renderer: renderer, store: store);
        var session = new Session { Id = "sess-1", WorkflowName = "wf", Mode = OrchestratorMode.Dev };
        var step = new StepDefinition
        {
            Name = "var-script",
            Agent = AgentType.Script,
            Role = "tester",
            PromptTemplate = "",
            ScriptCommand = "cmd /c echo {{project}}",
            OutputArtifactName = "output",
        };

        var result = await executor.ExecuteAsync(session, step, _ => throw new("unused"), default);

        Assert.True(result.Success);
        Assert.Contains("MyProject_RENDERED", result.ArtifactContent);
    }

    [Fact]
    public async Task ExecuteScriptStep_MissingScriptCommand_Throws()
    {
        var renderer = Substitute.For<IPromptRenderer>();
        renderer.Render(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns("");

        var executor = CreateExecutor(renderer: renderer);
        var session = new Session { Id = "sess-1", WorkflowName = "wf", Mode = OrchestratorMode.Dev };
        var step = new StepDefinition
        {
            Name = "empty-script",
            Agent = AgentType.Script,
            Role = "tester",
            PromptTemplate = "",
            ScriptCommand = null,
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(session, step, _ => throw new("unused"), default));

        Assert.Contains("no ScriptCommand", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteScriptStep_StoresArtifact_WhenOutputNameSet()
    {
        var renderer = Substitute.For<IPromptRenderer>();
        renderer.Render(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns("cmd /c echo ARTIFACT_CONTENT");

        var store = Substitute.For<IArtifactStore>();
        store.GetPath(Arg.Any<Session>(), "build-output").Returns("/path/to/build-output.md");

        var executor = CreateExecutor(renderer: renderer, store: store);
        var session = new Session { Id = "sess-1", WorkflowName = "wf", Mode = OrchestratorMode.Dev };
        var step = new StepDefinition
        {
            Name = "build",
            Agent = AgentType.Script,
            Role = "verifier",
            PromptTemplate = "",
            ScriptCommand = "cmd /c echo ARTIFACT_CONTENT",
            OutputArtifactName = "build-output",
        };

        var result = await executor.ExecuteAsync(session, step, _ => throw new("unused"), default);

        Assert.True(result.Success);
        Assert.Equal("/path/to/build-output.md", result.ArtifactPath);
        Assert.Contains("ARTIFACT_CONTENT", result.ArtifactContent);
        await store.Received(1).SaveAsync(session, "build-output", Arg.Is<string>(s => s.Contains("ARTIFACT_CONTENT")));
    }
}
