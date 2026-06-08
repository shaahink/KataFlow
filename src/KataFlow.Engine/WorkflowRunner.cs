using System.Diagnostics;
using KataFlow.Core;
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
        using var activity = Diagnostics.ActivitySource.StartActivity(Diagnostics.SpanNames.WorkflowRun);
        activity?.SetTag(Diagnostics.Tags.WorkflowName, workflow.Name);

        var session = await _sessionManager.ResolveAsync(workflow, ctx);
        activity?.SetTag(Diagnostics.Tags.SessionId, session.Id);

        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            [Diagnostics.Tags.SessionId] = session.Id,
            [Diagnostics.Tags.WorkflowName] = session.WorkflowName,
        });

        _logger.LogInformation("Starting workflow {WorkflowName} session {SessionId}",
            session.WorkflowName, session.Id);

        foreach (var step in workflow.Steps.Skip(session.CurrentStepIndex))
        {
            if (ct.IsCancellationRequested)
                return await _sessionManager.CancelAsync(session);

            if (step.Type == StepType.Parallel)
            {
                _logger.LogWarning("Parallel steps not supported in v1, skipping {Step}", step.Name);
                continue;
            }

            using var stepScope = _logger.BeginScope(new Dictionary<string, object>
            {
                [Diagnostics.Tags.StepName] = step.Name,
            });

            StepResult stepResult;

            if (session.Status == SessionStatus.WaitingApproval
                && session.History.Any(h => h.StepName == step.Name && h.Status == SessionStatus.Complete))
            {
                _logger.LogInformation("Resuming approval gate for step {StepName}", step.Name);
                var hist = session.History.Last(h => h.StepName == step.Name);
                var content = hist.OutputArtifactPath is not null && File.Exists(hist.OutputArtifactPath)
                    ? await File.ReadAllTextAsync(hist.OutputArtifactPath, ct)
                    : "";
                stepResult = new StepResult
                {
                    StepName = step.Name, Success = true,
                    ArtifactPath = hist.OutputArtifactPath,
                    ArtifactContent = content,
                };
                session.Status = SessionStatus.Running;
                await _sessionManager.PersistAsync(session);
            }
            else
            {
                _logger.LogInformation("Executing step {StepName} ({Role})", step.Name, step.Role);
                stepResult = await _stepExecutor.ExecuteAsync(session, step, _adapterResolver, ct);
            }

            if (!stepResult.Success)
            {
                _logger.LogError("Step {StepName} failed: {Error}", step.Name, stepResult.ErrorMessage);
                return await _sessionManager.FailAsync(session, stepResult.ErrorMessage!);
            }

            _logger.LogInformation("Step {StepName} completed", step.Name);

            var gateMode = ctx.AutoApprove ? ApprovalMode.Auto : step.Approval;
            if (!_gates.TryGetValue(gateMode, out var gate))
            {
                _logger.LogError("No approval gate registered for mode {Mode}", gateMode);
                return await _sessionManager.FailAsync(session, $"No gate for {gateMode}");
            }

            if (gateMode == ApprovalMode.Manual)
            {
                session.Status = SessionStatus.WaitingApproval;
                await _sessionManager.PersistAsync(session);
            }

            var decision = await gate.RequestApprovalAsync(stepResult, ct);

            if (decision == ApprovalDecision.Reject)
            {
                _logger.LogInformation("Step {StepName} rejected by operator", step.Name);
                return await _sessionManager.FailAsync(session, "Rejected by operator");
            }

            session.Status = SessionStatus.Running;
            session.CurrentStepIndex++;
            await _sessionManager.PersistAsync(session);
        }

        _logger.LogInformation("Workflow {WorkflowName} completed successfully", session.WorkflowName);
        return await _sessionManager.CompleteAsync(session);
    }
}
