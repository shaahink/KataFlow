namespace KataFlow.Core.Models;

public record AgentResponse
{
    public required string Content { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}
