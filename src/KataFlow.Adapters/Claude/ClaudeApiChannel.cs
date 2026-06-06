using System.Net.Http.Json;
using System.Text.Json.Serialization;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KataFlow.Adapters.Claude;

public class ClaudeApiChannel : IAgentChannel
{
    private readonly ILogger<ClaudeApiChannel> _logger;
    private readonly HttpClient _httpClient;
    private readonly ClaudeOptions _options;

    public ChannelType Type => ChannelType.ApiDirect;

    public ClaudeApiChannel(
        ILogger<ClaudeApiChannel> logger,
        HttpClient httpClient,
        IOptions<ClaudeOptions> options)
    {
        _logger = logger;
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<AgentResponse> SendAsync(AgentRequest request, CancellationToken ct = default)
    {
        try
        {
            var stepModel = request.Metadata.TryGetValue("model", out var m) ? m : _options.Model;

            var body = new ClaudeMessageRequest
            {
                Model = stepModel,
                MaxTokens = _options.MaxTokens,
                Messages = [new ClaudeMessage { Role = "user", Content = request.RenderedPrompt }],
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/v1/messages",
                body,
                ClaudeJsonOptions.Default,
                ct);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ClaudeMessageResponse>(
                ClaudeJsonOptions.Default,
                ct);

            var content = result?.Content?
                .Where(c => c.Type == "text")
                .Select(c => c.Text)
                .FirstOrDefault() ?? "";

            return new AgentResponse
            {
                Content = content,
                Success = !string.IsNullOrWhiteSpace(content),
                Metadata = new()
                {
                    ["model"] = result?.Model ?? stepModel,
                    ["usage_input_tokens"] = result?.Usage?.InputTokens.ToString() ?? "",
                    ["usage_output_tokens"] = result?.Usage?.OutputTokens.ToString() ?? "",
                },
            };
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized
            || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogError("Claude API auth error: {Message}", ex.Message);
            return new AgentResponse
            {
                Content = "",
                Success = false,
                ErrorMessage = $"Claude API auth error: {ex.Message}",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("Claude API error: {Message}", ex.Message);
            return new AgentResponse
            {
                Content = "",
                Success = false,
                ErrorMessage = ex.Message,
            };
        }
    }

    private class ClaudeMessageRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("messages")]
        public List<ClaudeMessage> Messages { get; set; } = new();
    }

    private class ClaudeMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    private class ClaudeMessageResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("content")]
        public List<ClaudeContentBlock>? Content { get; set; }

        [JsonPropertyName("usage")]
        public ClaudeUsage? Usage { get; set; }
    }

    private class ClaudeContentBlock
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";
    }

    private class ClaudeUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }
}

internal static class ClaudeJsonOptions
{
    public static readonly System.Text.Json.JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };
}
