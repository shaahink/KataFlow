namespace KataFlow.Adapters.Rest;

public class RestOptions
{
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.deepseek.com";
    public string Model { get; set; } = "deepseek-chat";
    public int MaxTokens { get; set; } = 16384;
}
