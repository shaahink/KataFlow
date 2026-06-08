using KataFlow.Core.Enums;

namespace KataFlow.Core.Models;

public class Session
{
    public required string Id { get; init; }
    public required string WorkflowName { get; init; }
    public OrchestratorMode Mode { get; init; }
    public SessionStatus Status { get; set; } = SessionStatus.Running;
    public int CurrentStepIndex { get; set; }
    public Dictionary<string, string> Artifacts { get; } = new();
    public Dictionary<string, string> Variables { get; } = new();
    public List<SessionStep> History { get; } = new();
    public List<StepBudget> Budget { get; } = new();
    public decimal? BudgetCapUsd { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public decimal TotalCostUsd => Budget.Sum(b => b.CostUsd);

    [System.Text.Json.Serialization.JsonIgnore]
    public int TotalInputTokens => Budget.Sum(b => b.InputTokens);

    [System.Text.Json.Serialization.JsonIgnore]
    public int TotalOutputTokens => Budget.Sum(b => b.OutputTokens);
}
