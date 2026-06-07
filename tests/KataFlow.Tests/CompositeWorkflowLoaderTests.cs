using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using KataFlow.Engine.Loaders;
using NSubstitute;

namespace KataFlow.Tests;

[Trait("Category", "Unit")]
public class CompositeWorkflowLoaderTests
{
    [Fact]
    public void Load_TriesPresetFirst()
    {
        var preset = Substitute.For<IWorkflowLoader>();
        preset.ListAvailable().Returns(["my-workflow"]);
        preset.Load("my-workflow").Returns(new WorkflowDefinition
        {
            Name = "my-workflow",
            Steps = new List<StepDefinition> { new() { Name = "s1", Agent = AgentType.Rest, Role = "exec", PromptTemplate = "t.md" } }.AsReadOnly(),
        });

        var yaml = Substitute.For<IWorkflowLoader>();
        yaml.ListAvailable().Returns([]);

        var composite = new CompositeWorkflowLoader([preset, yaml]);
        var result = composite.Load("my-workflow");

        Assert.Equal("my-workflow", result.Name);
        preset.Received(1).Load("my-workflow");
        yaml.DidNotReceive().Load(Arg.Any<string>());
    }

    [Fact]
    public void ListAvailable_Deduplicates()
    {
        var preset = Substitute.For<IWorkflowLoader>();
        preset.ListAvailable().Returns(["a", "b"]);
        var yaml = Substitute.For<IWorkflowLoader>();
        yaml.ListAvailable().Returns(["b", "c"]);

        var composite = new CompositeWorkflowLoader([preset, yaml]);
        var list = composite.ListAvailable();

        Assert.Equal(3, list.Count);
        Assert.Contains("a", list);
        Assert.Contains("b", list);
        Assert.Contains("c", list);
    }
}
