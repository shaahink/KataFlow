using KataFlow.Core.Enums;

namespace KataFlow.Core.Models;

public record SessionContext
{
    public string? SessionId { get; init; }
    public OrchestratorMode Mode { get; init; } = OrchestratorMode.Dev;
    public Dictionary<string, string> Variables { get; init; } = new();
    public bool AutoApprove { get; init; }
}
