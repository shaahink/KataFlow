namespace KataFlow.Core.Models;

public record SessionResult
{
    public required string SessionId { get; init; }
    public bool Success { get; init; }
    public IReadOnlyList<StepResult> Steps { get; init; } = [];
    public string? ErrorMessage { get; init; }
}

public record StepResult
{
    public required string StepName { get; init; }
    public bool Success { get; init; }
    public string? ArtifactPath { get; init; }
    public string? ArtifactContent { get; init; }
    public string? ErrorMessage { get; init; }
    public StepBudget? Budget { get; init; }
}
