using KataFlow.Core.Enums;

namespace KataFlow.Core.Models;

public record WorkflowDefinition
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public OrchestratorMode DefaultMode { get; init; } = OrchestratorMode.Dev;
    public required IReadOnlyList<StepDefinition> Steps { get; init; }
    public Dictionary<string, string> Variables { get; init; } = new();
}
