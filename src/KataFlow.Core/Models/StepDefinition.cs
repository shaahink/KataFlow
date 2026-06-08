using KataFlow.Core.Enums;

namespace KataFlow.Core.Models;

public record StepDefinition
{
    public required string Name { get; init; }
    public required AgentType Agent { get; init; }
    public required string Role { get; init; }
    public required string PromptTemplate { get; init; }
    public string? Model { get; init; }
    public ChannelType? ChannelOverride { get; init; }
    public ApprovalMode Approval { get; init; } = ApprovalMode.Manual;
    public IReadOnlyList<string> ContextArtifacts { get; init; } = [];
    public string? OutputArtifactName { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(10);
    public int MaxRetries { get; init; } = 1;
    public StepType Type { get; init; } = StepType.Sequential;
    public IReadOnlyList<string> DependsOn { get; init; } = [];
    public int WorkerCount { get; init; } = 1;
    public bool UseWorktree { get; init; }
    public string? ScriptCommand { get; init; }
}
