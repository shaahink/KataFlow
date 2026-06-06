using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using KataFlow.Engine.Gates;
using Microsoft.Extensions.Logging;

namespace KataFlow.Engine;

public class WorkflowRunner : IWorkflowRunner
{
    private readonly ISessionStore _sessionStore;
    private readonly StepExecutor _stepExecutor;
    private readonly AutoApprovalGate _autoGate;
    private readonly ManualApprovalGate _manualGate;
    private readonly ILogger<WorkflowRunner> _logger;
    private readonly Func<AgentType, IAgentAdapter> _adapterResolver;

    public WorkflowRunner(
        ISessionStore sessionStore,
        StepExecutor stepExecutor,
        AutoApprovalGate autoGate,
        ManualApprovalGate manualGate,
        ILogger<WorkflowRunner> logger,
        Func<AgentType, IAgentAdapter> adapterResolver)
    {
        _sessionStore = sessionStore;
        _stepExecutor = stepExecutor;
        _autoGate = autoGate;
        _manualGate = manualGate;
        _logger = logger;
        _adapterResolver = adapterResolver;
    }

    public async Task<SessionResult> RunAsync(
        WorkflowDefinition workflow,
        SessionContext ctx,
        CancellationToken ct = default)
    {
        var session = ctx.SessionId is not null
            ? await _sessionStore.GetAsync(ctx.SessionId)
                ?? throw new InvalidOperationException($"Session not found: {ctx.SessionId}")
            : await _sessionStore.CreateAsync(workflow.Name, ctx.Mode);

        foreach (var (k, v) in ctx.Variables)
            session.Variables[k] = v;

        await _sessionStore.SaveAsync(session);

        foreach (var step in workflow.Steps.Skip(session.CurrentStepIndex))
        {
            if (ct.IsCancellationRequested)
            {
                session.Status = SessionStatus.Cancelled;
                await _sessionStore.SaveAsync(session);
                return new SessionResult { SessionId = session.Id, Success = false, ErrorMessage = "Cancelled" };
            }

            if (step.Type == StepType.Parallel)
            {
                _logger.LogWarning("Parallel steps not supported in v1, skipping {Step}", step.Name);
                continue;
            }

            var stepResult = await _stepExecutor.ExecuteAsync(session, step, _adapterResolver, ct);

            if (!stepResult.Success)
            {
                session.Status = SessionStatus.Failed;
                await _sessionStore.SaveAsync(session);
                return new SessionResult
                {
                    SessionId = session.Id,
                    Success = false,
                    ErrorMessage = stepResult.ErrorMessage
                };
            }

            IApprovalGate gate = step.Approval == ApprovalMode.Auto || ctx.AutoApprove
                ? _autoGate
                : _manualGate;

            var decision = await gate.RequestApprovalAsync(stepResult, ct);

            if (decision == ApprovalDecision.Reject)
            {
                session.Status = SessionStatus.Failed;
                await _sessionStore.SaveAsync(session);
                return new SessionResult
                {
                    SessionId = session.Id,
                    Success = false,
                    ErrorMessage = "Rejected by operator"
                };
            }

            session.CurrentStepIndex++;
            await _sessionStore.SaveAsync(session);
        }

        session.Status = SessionStatus.Complete;
        session.CompletedAt = DateTimeOffset.UtcNow;
        await _sessionStore.SaveAsync(session);
        return new SessionResult { SessionId = session.Id, Success = true };
    }
}
