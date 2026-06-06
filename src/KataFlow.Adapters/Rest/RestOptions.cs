namespace KataFlow.Adapters.Rest;

public class RestOptions
{
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.deepseek.com";
    public string Model { get; set; } = "deepseek-chat";
    public int MaxTokens { get; set; } = 16384;
    public FileDropOptions FileDrop { get; set; } = new();
}

public class FileDropOptions
{
    public int WatchTimeoutMinutes { get; set; } = 30;
    public int PollIntervalMs { get; set; } = 500;
}
