using KataFlow.Core.Enums;
using KataFlow.Core.Models;

namespace KataFlow.Engine.Presets;

internal static class SoftwareLifecyclePreset
{
    public static WorkflowDefinition Build() => WorkflowBuilder
        .Create("software-lifecycle")
        .WithDescription("Plan → implement → review → report cycle for feature development")
        .WithDefaultMode(OrchestratorMode.Dev)
        .AddStep(s => s
            .Named("plan")
            .UseAgent(AgentType.Claude)
            .WithRole("planner")
            .WithTemplate("templates/engineering/planner.md")
            .ViaFileDrop()
            .RequireApproval()
            .OutputAs("plan")
            .WithTimeout(TimeSpan.FromMinutes(15)))
        .AddStep(s => s
            .Named("implement")
            .UseAgent(AgentType.Rest)
            .WithRole("executor")
            .WithTemplate("templates/engineering/executor.md")
            .WithContext("plan")
            .ViaFileDrop()
            .AutoApprove()
            .OutputAs("implementation")
            .WithTimeout(TimeSpan.FromMinutes(30)))
        .AddStep(s => s
            .Named("review")
            .UseAgent(AgentType.Claude)
            .WithRole("reviewer")
            .WithTemplate("templates/engineering/reviewer.md")
            .WithContext("plan", "implementation")
            .ViaFileDrop()
            .RequireApproval()
            .OutputAs("review")
            .WithTimeout(TimeSpan.FromMinutes(10)))
        .AddStep(s => s
            .Named("report")
            .UseAgent(AgentType.Claude)
            .WithRole("reporter")
            .WithTemplate("templates/engineering/reporter.md")
            .WithContext("plan", "implementation", "review")
            .ViaFileDrop()
            .AutoApprove()
            .OutputAs("report")
            .WithTimeout(TimeSpan.FromMinutes(10)))
        .Build();
}
