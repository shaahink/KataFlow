using KataFlow.Core.Enums;
using KataFlow.Core.Models;

namespace KataFlow.Engine.Presets;

internal static class PlannerOnlyPreset
{
    public static WorkflowDefinition Build() => WorkflowBuilder
        .Create("planner-only")
        .WithDescription("Planning only — single step")
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
        .Build();
}
