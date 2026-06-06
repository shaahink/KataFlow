using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using Microsoft.Extensions.Logging;

namespace KataFlow.Engine.Gates;

public class AutoApprovalGate : IApprovalGate
{
    private readonly ILogger<AutoApprovalGate> _logger;

    public ApprovalMode Mode => ApprovalMode.Auto;

    public AutoApprovalGate(ILogger<AutoApprovalGate> logger)
    {
        _logger = logger;
    }

    public Task<ApprovalDecision> RequestApprovalAsync(StepResult result, CancellationToken ct = default)
    {
        _logger.LogInformation("AUTO-APPROVED step:{Step}", result.StepName);
        return Task.FromResult(ApprovalDecision.Approve);
    }
}
