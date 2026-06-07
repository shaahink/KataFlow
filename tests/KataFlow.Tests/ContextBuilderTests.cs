using KataFlow.Core.Abstractions;
using KataFlow.Core.Enums;
using KataFlow.Core.Models;
using KataFlow.Engine;
using NSubstitute;

namespace KataFlow.Tests;

[Trait("Category", "Unit")]
public class ContextBuilderTests
{
    [Fact]
    public void Build_IncludesReservedVars()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.GetCurrentDirectory().Returns("/test");
        fs.Combine(Arg.Any<string>(), Arg.Any<string>())
            .Returns(c => Path.Combine(c.ArgAt<string>(0), c.ArgAt<string>(1)));
        fs.Combine(Arg.Any<string[]>()).Returns(c => Path.Combine(c.Arg<string[]>()));
        var builder = new ContextBuilder(fs);

        var session = new Session
        {
            Id = "sess-1",
            WorkflowName = "test-wf",
            Mode = OrchestratorMode.Dev,
        };
        var step = new StepDefinition
        {
            Name = "plan",
            Agent = AgentType.Claude,
            Role = "planner",
            PromptTemplate = "template.md",
        };

        var vars = builder.Build(session, step);

        Assert.Equal("sess-1", vars["_session_id"]);
        Assert.Equal("plan", vars["_step_name"]);
        Assert.Equal("test-wf", vars["_workflow_name"]);
        var expectedPath = Path.Combine("/test", "sessions", "sess-1", "output-plan.md");
        Assert.Equal(expectedPath, vars["_output_path"]);
    }

    [Fact]
    public void Build_IncludesArtifactContent()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.GetCurrentDirectory().Returns("/test");
        fs.ReadAllTextAsync("/test/sessions/sess-1/artifacts/plan.md", default)
            .Returns("## Plan content");

        var builder = new ContextBuilder(fs);

        var session = new Session
        {
            Id = "sess-1",
            WorkflowName = "test-wf",
            Mode = OrchestratorMode.Dev,
        };
        session.Artifacts["plan"] = "/test/sessions/sess-1/artifacts/plan.md";

        var step = new StepDefinition
        {
            Name = "implement",
            Agent = AgentType.Rest,
            Role = "executor",
            PromptTemplate = "template.md",
            ContextArtifacts = ["plan"],
        };

        var vars = builder.Build(session, step);

        Assert.Equal("## Plan content", vars["plan"]);
    }

    [Fact]
    public void Build_SessionVariablesOverrideDefaults()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.GetCurrentDirectory().Returns("/test");
        var builder = new ContextBuilder(fs);

        var session = new Session
        {
            Id = "sess-1",
            WorkflowName = "test-wf",
            Mode = OrchestratorMode.Dev,
        };
        session.Variables["project"] = "MyFeature";

        var step = new StepDefinition
        {
            Name = "plan",
            Agent = AgentType.Claude,
            Role = "planner",
            PromptTemplate = "template.md",
        };

        var vars = builder.Build(session, step);

        Assert.Equal("MyFeature", vars["project"]);
    }
}
