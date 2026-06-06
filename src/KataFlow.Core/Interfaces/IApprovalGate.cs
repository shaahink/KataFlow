using KataFlow.Core.Enums;
using KataFlow.Core.Models;

namespace KataFlow.Core.Interfaces;

public interface IApprovalGate
{
    Task<ApprovalDecision> RequestApprovalAsync(
        StepResult result,
        CancellationToken ct = default);
}
