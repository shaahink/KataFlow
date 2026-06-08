namespace KataFlow.Core.Models;

public class StepBudget
{
    public required string StepName { get; init; }
    public string Model { get; init; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal CostUsd { get; set; }
}
