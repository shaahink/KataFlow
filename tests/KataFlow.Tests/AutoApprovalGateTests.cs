using KataFlow.Core.Enums;
using KataFlow.Core.Models;
using KataFlow.Engine.Gates;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace KataFlow.Tests;

[Trait("Category", "Unit")]
public class AutoApprovalGateTests
{
    [Fact]
    public async Task RequestApprovalAsync_ReturnsApprove()
    {
        var gate = new AutoApprovalGate(Substitute.For<ILogger<AutoApprovalGate>>());
        var result = new StepResult { StepName = "test", Success = true };

        var decision = await gate.RequestApprovalAsync(result);

        Assert.Equal(ApprovalDecision.Approve, decision);
    }

    [Fact]
    public void Mode_ReturnsAuto()
    {
        var gate = new AutoApprovalGate(Substitute.For<ILogger<AutoApprovalGate>>());
        Assert.Equal(ApprovalMode.Auto, gate.Mode);
    }
}
