using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using Microsoft.Extensions.Logging;

namespace KataFlow.Engine;

public class WorkflowRunner : IWorkflowRunner
{
    private readonly SessionManager _sessionManager;
    private readonly StepExecutor _stepExecutor;
    private readonly ILogger<WorkflowRunner> _logger;
    private readonly Func<AgentType, IAgentAdapter> _adapterResolver;
    private readonly IReadOnlyDictionary<ApprovalMode, IApprovalGate> _gates;

    public WorkflowRunner(
        SessionManager sessionManager,
        StepExecutor stepExecutor,
        ILogger<WorkflowRunner> logger,
        Func<AgentType, IAgentAdapter> adapterResolver,
        IEnumerable<IApprovalGate> gates)
    {
        _sessionManager = sessionManager;
        _stepExecutor = stepExecutor;
        _logger = logger;
        _adapterResolver = adapterResolver;
        _gates = gates.ToDictionary(g => g.Mode);
    }

    public async Task<SessionResult> RunAsync(
        WorkflowDefinition workflow,
        SessionContext ctx,
        CancellationToken ct = default)
    {
        var session = await _sessionManager.ResolveAsync(workflow, ctx);

        foreach (var step in workflow.Steps.Skip(session.CurrentStepIndex))
        {
            if (ct.IsCancellationRequested)
                return await _sessionManager.CancelAsync(session);

            if (step.Type == StepType.Parallel)
            {
                _logger.LogWarning("Parallel steps not supported in v1, skipping {Step}", step.Name);
                continue;
            }

            var stepResult = await _stepExecutor.ExecuteAsync(session, step, _adapterResolver, ct);

            if (!stepResult.Success)
                return await _sessionManager.FailAsync(session, stepResult.ErrorMessage!);

            var gateMode = ctx.AutoApprove ? ApprovalMode.Auto : step.Approval;
            if (!_gates.TryGetValue(gateMode, out var gate))
            {
                _logger.LogError("No approval gate registered for mode {Mode}", gateMode);
                return await _sessionManager.FailAsync(session, $"No gate for {gateMode}");
            }

            var decision = await gate.RequestApprovalAsync(stepResult, ct);

            if (decision == ApprovalDecision.Reject)
                return await _sessionManager.FailAsync(session, "Rejected by operator");

            session.CurrentStepIndex++;
            await _sessionManager.PersistAsync(session);
        }

        return await _sessionManager.CompleteAsync(session);
    }
}
