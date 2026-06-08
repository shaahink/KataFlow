using KataFlow.Core.Enums;
using KataFlow.Core.Models;

namespace KataFlow.Engine.Presets;

internal static class BugFixPreset
{
    public static WorkflowDefinition Build() => WorkflowBuilder
        .Create("bug-fix")
        .WithDescription("Diagnose bug (Claude) → fix (CLI agent) → verify (tests) → report")
        .WithDefaultMode(OrchestratorMode.Dev)
        .AddStep(s => s
            .Named("diagnose")
            .UseAgent(AgentType.Claude)
            .WithRole("planner")
            .WithTemplate("templates/agentic/diagnoser.md")
            .ViaFileDrop()
            .RequireApproval()
            .OutputAs("diagnosis")
            .WithTimeout(TimeSpan.FromMinutes(10)))
        .AddStep(s => s
            .Named("fix")
            .UseAgent(AgentType.Rest)
            .WithRole("executor")
            .WithTemplate("templates/agentic/fixer.md")
            .WithContext("diagnosis")
            .ViaCliExecute()
            .RequireApproval()
            .OutputAs("fix")
            .WithTimeout(TimeSpan.FromMinutes(45)))
        .AddStep(s => s
            .Named("verify")
            .UseAgent(AgentType.Script)
            .WithRole("tester")
            .WithTemplate("")
            .AsScript("{{test_command}}")
            .AutoApprove()
            .OutputAs("test-results")
            .WithTimeout(TimeSpan.FromMinutes(10)))
        .AddStep(s => s
            .Named("report")
            .UseAgent(AgentType.Claude)
            .WithRole("reporter")
            .WithTemplate("templates/agentic/reporter.md")
            .WithContext("diagnosis", "fix", "test-results")
            .ViaFileDrop()
            .AutoApprove()
            .OutputAs("report")
            .WithTimeout(TimeSpan.FromMinutes(10)))
        .Build();
}
