using KataFlow.Core.Enums;
using KataFlow.Core.Models;

namespace KataFlow.Engine.Presets;

internal static class QuickExecutePreset
{
    public static WorkflowDefinition Build() => WorkflowBuilder
        .Create("quick-execute")
        .WithDescription("Single-step executor handoff")
        .WithDefaultMode(OrchestratorMode.Dev)
        .AddStep(s => s
            .Named("implement")
            .UseAgent(AgentType.Rest)
            .WithRole("executor")
            .WithTemplate("templates/engineering/executor.md")
            .ViaFileDrop()
            .AutoApprove()
            .OutputAs("implementation")
            .WithTimeout(TimeSpan.FromMinutes(30)))
        .Build();
}
