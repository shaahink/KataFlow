using KataFlow.Core.Enums;
using KataFlow.Core.Models;

namespace KataFlow.Engine.Presets;

internal static class ReviewOnlyPreset
{
    public static WorkflowDefinition Build() => WorkflowBuilder
        .Create("review-only")
        .WithDescription("Review an existing artifact (pass via --context implementation=./path)")
        .WithDefaultMode(OrchestratorMode.Headless)
        .AddStep(s => s
            .Named("review")
            .UseAgent(AgentType.Claude)
            .WithRole("reviewer")
            .WithTemplate("templates/engineering/reviewer.md")
            .WithContext("implementation")
            .ViaFileDrop()
            .RequireApproval()
            .OutputAs("review")
            .WithTimeout(TimeSpan.FromMinutes(10)))
        .Build();
}
