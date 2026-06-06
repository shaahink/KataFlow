using KataFlow.Core.Enums;
using KataFlow.Core.Models;

namespace KataFlow.Engine.Presets;

internal static class TradingStrategyPreset
{
    public static WorkflowDefinition Build() => WorkflowBuilder
        .Create("trading-strategy")
        .WithDescription("Generate, test, review, and report on a trading strategy")
        .WithDefaultMode(OrchestratorMode.Dev)
        .AddStep(s => s
            .Named("generate-strategy")
            .UseAgent(AgentType.Claude)
            .WithRole("planner")
            .WithTemplate("templates/trading/strategy-generator.md")
            .ViaFileDrop()
            .RequireApproval()
            .OutputAs("strategy")
            .WithTimeout(TimeSpan.FromMinutes(15)))
        .AddStep(s => s
            .Named("test-strategy")
            .UseAgent(AgentType.Rest)
            .WithRole("executor")
            .WithTemplate("templates/trading/strategy-tester.md")
            .WithContext("strategy")
            .ViaFileDrop()
            .AutoApprove()
            .OutputAs("test-results")
            .WithTimeout(TimeSpan.FromMinutes(45)))
        .AddStep(s => s
            .Named("review-strategy")
            .UseAgent(AgentType.Claude)
            .WithRole("reviewer")
            .WithTemplate("templates/trading/strategy-reviewer.md")
            .WithContext("strategy", "test-results")
            .ViaFileDrop()
            .RequireApproval()
            .OutputAs("review")
            .WithTimeout(TimeSpan.FromMinutes(10)))
        .AddStep(s => s
            .Named("write-report")
            .UseAgent(AgentType.Claude)
            .WithRole("reporter")
            .WithTemplate("templates/trading/reporter.md")
            .WithContext("strategy", "test-results", "review")
            .ViaFileDrop()
            .AutoApprove()
            .OutputAs("report")
            .WithTimeout(TimeSpan.FromMinutes(10)))
        .Build();
}
