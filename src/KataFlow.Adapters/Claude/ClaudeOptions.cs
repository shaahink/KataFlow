namespace KataFlow.Adapters.Claude;

public class ClaudeOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-sonnet-4-6";
    public int MaxTokens { get; set; } = 16384;
}
