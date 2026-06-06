using KataFlow.Core.Abstractions;
using KataFlow.Core.Enums;
using KataFlow.Engine.Loaders;
using KataFlow.Infrastructure;

namespace KataFlow.Tests;

public class YamlWorkflowLoaderIntegrationTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly IFileSystem _fs;

    public YamlWorkflowLoaderIntegrationTests()
    {
        _fs = new SystemFileSystem();
        _fs.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Load_ValidYaml_ParsesSteps()
    {
        var yaml = """
            workflow:
              name: test-workflow
              description: "A test"
              default_mode: dev
              steps:
                - name: plan
                  agent: claude
                  role: planner
                  prompt_template: templates/plan.md
                  approval: manual
                  output_artifact: plan
                  timeout_minutes: 15
                - name: implement
                  agent: rest
                  role: executor
                  prompt_template: templates/implement.md
                  context_artifacts: [plan]
                  approval: auto
                  output_artifact: implementation
                  timeout_minutes: 30
            """;

        var path = _fs.Combine(_tempDir, "test.yaml");
        _fs.WriteAllText(path, yaml);

        var loader = new YamlWorkflowLoader(_fs, _tempDir);
        var workflow = loader.Load("test");

        Assert.Equal("test-workflow", workflow.Name);
        Assert.Equal("A test", workflow.Description);
        Assert.Equal(OrchestratorMode.Dev, workflow.DefaultMode);
        Assert.Equal(2, workflow.Steps.Count);

        var first = workflow.Steps[0];
        Assert.Equal("plan", first.Name);
        Assert.Equal(AgentType.Claude, first.Agent);
        Assert.Equal("planner", first.Role);
        Assert.Equal("templates/plan.md", first.PromptTemplate);
        Assert.Equal(ApprovalMode.Manual, first.Approval);
        Assert.Equal("plan", first.OutputArtifactName);
        Assert.Equal(15, first.Timeout.TotalMinutes);

        var second = workflow.Steps[1];
        Assert.Equal("implement", second.Name);
        Assert.Equal(AgentType.Rest, second.Agent);
        Assert.Single(second.ContextArtifacts);
        Assert.Equal("plan", second.ContextArtifacts[0]);
        Assert.Equal(ApprovalMode.Auto, second.Approval);
    }

    [Fact]
    public void Load_HeadlessMode_ParsesCorrectly()
    {
        var yaml = """
            workflow:
              name: headless-test
              default_mode: headless
              steps:
                - name: review
                  agent: claude
                  role: reviewer
                  prompt_template: templates/review.md
            """;

        var path = _fs.Combine(_tempDir, "headless.yaml");
        _fs.WriteAllText(path, yaml);

        var loader = new YamlWorkflowLoader(_fs, _tempDir);
        var workflow = loader.Load("headless");

        Assert.Equal(OrchestratorMode.Headless, workflow.DefaultMode);
    }

    [Fact]
    public void Load_MissingFile_Throws()
    {
        var loader = new YamlWorkflowLoader(_fs, _tempDir);
        Assert.Throws<FileNotFoundException>(() => loader.Load("nonexistent"));
    }

    [Fact]
    public void ListAvailable_ReturnsYamlFiles()
    {
        _fs.WriteAllText(_fs.Combine(_tempDir, "wf1.yaml"), "workflow:\n  name: wf1\n  steps: []");
        _fs.WriteAllText(_fs.Combine(_tempDir, "wf2.yml"), "workflow:\n  name: wf2\n  steps: []");

        var loader = new YamlWorkflowLoader(_fs, _tempDir);
        var list = loader.ListAvailable();

        Assert.Contains("wf1", list);
        Assert.Contains("wf2", list);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
