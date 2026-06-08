using KataFlow.Core.Enums;
using KataFlow.Core.Models;

namespace KataFlow.Engine.Presets;

internal static class CodeReviewAgenticPreset
{
    public static WorkflowDefinition Build() => WorkflowBuilder
        .Create("code-review-agentic")
        .WithDescription("Review an implementation artifact and produce a structured report")
        .WithDefaultMode(OrchestratorMode.Headless)
        .AddStep(s => s
            .Named("review")
            .UseAgent(AgentType.Claude)
            .WithRole("reviewer")
            .WithTemplate("templates/agentic/reviewer.md")
            .ViaFileDrop()
            .RequireApproval()
            .OutputAs("review")
            .WithTimeout(TimeSpan.FromMinutes(10)))
        .AddStep(s => s
            .Named("report")
            .UseAgent(AgentType.Claude)
            .WithRole("reporter")
            .WithTemplate("templates/agentic/reporter.md")
            .WithContext("review")
            .ViaFileDrop()
            .AutoApprove()
            .OutputAs("report")
            .WithTimeout(TimeSpan.FromMinutes(10)))
        .Build();
}
