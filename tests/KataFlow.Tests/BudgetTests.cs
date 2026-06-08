using KataFlow.Core;
using KataFlow.Core.Models;

namespace KataFlow.Tests;

[Trait("Category", "Unit")]
public class BudgetTests
{
    [Fact]
    public void ModelPricing_KnownModel_CalculatesCorrectly()
    {
        var cost = ModelPricing.Calculate("claude-sonnet-4-6", 1000, 500);
        var expected = (1000 * 3.00m + 500 * 15.00m) / 1_000_000m;
        Assert.Equal(expected, cost);
    }

    [Fact]
    public void ModelPricing_UnknownModel_ReturnsZero()
    {
        var cost = ModelPricing.Calculate("unknown-model", 1000, 500);
        Assert.Equal(0m, cost);
    }

    [Fact]
    public void ModelPricing_DeepSeekChat_CalculatesCorrectly()
    {
        var cost = ModelPricing.Calculate("deepseek-chat", 10000, 5000);
        var expected = (10000m * 0.14m + 5000m * 0.28m) / 1_000_000m;
        Assert.Equal(expected, cost);
    }

    [Fact]
    public void ModelPricing_IsKnown_ReturnsTrueForKnownModel()
    {
        Assert.True(ModelPricing.IsKnown("claude-sonnet-4-6"));
        Assert.True(ModelPricing.IsKnown("gpt-4o"));
        Assert.False(ModelPricing.IsKnown("nonexistent"));
    }

    [Fact]
    public void Session_TotalCostUsd_SumsStepBudgets()
    {
        var session = new Session { Id = "s1", WorkflowName = "wf" };
        session.Budget.Add(new StepBudget { StepName = "plan", Model = "claude-sonnet-4-6", InputTokens = 1000, OutputTokens = 500, CostUsd = 0.0105m });
        session.Budget.Add(new StepBudget { StepName = "review", Model = "claude-sonnet-4-6", InputTokens = 2000, OutputTokens = 300, CostUsd = 0.0105m });

        Assert.Equal(0.0210m, session.TotalCostUsd);
        Assert.Equal(3000, session.TotalInputTokens);
        Assert.Equal(800, session.TotalOutputTokens);
    }

    [Fact]
    public void Session_TotalCostUsd_EmptyBudget_ReturnsZero()
    {
        var session = new Session { Id = "s1", WorkflowName = "wf" };
        Assert.Equal(0m, session.TotalCostUsd);
        Assert.Equal(0, session.TotalInputTokens);
        Assert.Equal(0, session.TotalOutputTokens);
    }

    [Fact]
    public void Session_BudgetCapUsd_DefaultsToNull()
    {
        var session = new Session { Id = "s1", WorkflowName = "wf" };
        Assert.Null(session.BudgetCapUsd);
    }

    [Fact]
    public void StepBudget_HoldsCorrectValues()
    {
        var budget = new StepBudget
        {
            StepName = "plan",
            Model = "claude-sonnet-4-6",
            InputTokens = 500,
            OutputTokens = 200,
            CostUsd = 0.0045m,
        };

        Assert.Equal("plan", budget.StepName);
        Assert.Equal("claude-sonnet-4-6", budget.Model);
        Assert.Equal(500, budget.InputTokens);
        Assert.Equal(200, budget.OutputTokens);
        Assert.Equal(0.0045m, budget.CostUsd);
    }
}
