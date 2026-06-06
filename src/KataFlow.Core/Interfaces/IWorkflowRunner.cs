using KataFlow.Core.Models;

namespace KataFlow.Core.Interfaces;

public interface IWorkflowRunner
{
    Task<SessionResult> RunAsync(
        WorkflowDefinition workflow,
        SessionContext context,
        CancellationToken ct = default);
}
