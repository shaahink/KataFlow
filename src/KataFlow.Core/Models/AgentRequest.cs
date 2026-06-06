namespace KataFlow.Core.Models;

public record AgentRequest
{
    public required string SessionId { get; init; }
    public required string StepName { get; init; }
    public required string RenderedPrompt { get; init; }
    public IReadOnlyDictionary<string, string> ContextFiles { get; init; } = new Dictionary<string, string>();
    public Dictionary<string, string> Metadata { get; init; } = new();
}
