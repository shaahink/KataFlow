using KataFlow.Core.Enums;

namespace KataFlow.Core.Models;

public class SessionStep
{
    public required string StepName { get; init; }
    public SessionStatus Status { get; set; }
    public string? OutputArtifactPath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
