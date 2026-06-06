namespace KataFlow.Core.Models;

public class KataFlowConfig
{
    public string WorkflowsPath { get; set; } = "./workflows";
    public string TemplatesPath { get; set; } = "./templates";
    public string SessionsPath { get; set; } = "./sessions";
    public string DefaultMode { get; set; } = "dev";
}

public class AgentClaudeConfig
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-sonnet-4-6";
    public int MaxTokens { get; set; } = 16384;
    public FileDropOptions FileDrop { get; set; } = new();
}

public class AgentRestConfig
{
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.deepseek.com";
    public string Model { get; set; } = "deepseek-chat";
    public int MaxTokens { get; set; } = 16384;
    public FileDropOptions FileDrop { get; set; } = new();
}

public class FileDropOptions
{
    public int WatchTimeoutMinutes { get; set; } = 15;
    public int PollIntervalMs { get; set; } = 500;
}
