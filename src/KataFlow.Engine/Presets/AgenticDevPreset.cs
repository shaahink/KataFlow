using KataFlow.Core.Enums;
using KataFlow.Core.Models;

namespace KataFlow.Engine.Presets;

internal static class AgenticDevPreset
{
    public static WorkflowDefinition Build() => WorkflowBuilder
        .Create("agentic-dev")
        .WithDescription("Standard agentic programming: plan (Claude) → implement (CLI agent) → build → test → review (Claude) → report")
        .WithDefaultMode(OrchestratorMode.Dev)
        .AddStep(s => s
            .Named("plan")
            .UseAgent(AgentType.Claude)
            .WithRole("planner")
            .WithTemplate("templates/agentic/planner.md")
            .ViaFileDrop()
            .RequireApproval()
            .OutputAs("plan")
            .WithTimeout(TimeSpan.FromMinutes(15)))
        .AddStep(s => s
            .Named("implement")
            .UseAgent(AgentType.Rest)
            .WithRole("executor")
            .WithTemplate("templates/agentic/executor.md")
            .WithContext("plan")
            .ViaCliExecute()
            .RequireApproval()
            .OutputAs("implementation")
            .WithTimeout(TimeSpan.FromMinutes(60)))
        .AddStep(s => s
            .Named("build")
            .UseAgent(AgentType.Script)
            .WithRole("verifier")
            .WithTemplate("")
            .AsScript("{{build_command}}")
            .AutoApprove()
            .OutputAs("build-output")
            .WithTimeout(TimeSpan.FromMinutes(5)))
        .AddStep(s => s
            .Named("test")
            .UseAgent(AgentType.Script)
            .WithRole("tester")
            .WithTemplate("")
            .AsScript("{{test_command}}")
            .AutoApprove()
            .OutputAs("test-results")
            .WithTimeout(TimeSpan.FromMinutes(10)))
        .AddStep(s => s
            .Named("review")
            .UseAgent(AgentType.Claude)
            .WithRole("reviewer")
            .WithTemplate("templates/agentic/reviewer.md")
            .WithContext("plan", "implementation", "build-output", "test-results")
            .ViaFileDrop()
            .RequireApproval()
            .OutputAs("review")
            .WithTimeout(TimeSpan.FromMinutes(10)))
        .AddStep(s => s
            .Named("report")
            .UseAgent(AgentType.Claude)
            .WithRole("reporter")
            .WithTemplate("templates/agentic/reporter.md")
            .WithContext("plan", "implementation", "test-results", "review")
            .ViaFileDrop()
            .AutoApprove()
            .OutputAs("report")
            .WithTimeout(TimeSpan.FromMinutes(10)))
        .Build();
}
