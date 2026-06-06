using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using Microsoft.Extensions.Logging;

namespace KataFlow.Adapters.Rest;

public class RestApiChannel : IAgentChannel
{
    private readonly ILogger<RestApiChannel> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly int _maxTokens;

    public ChannelType Type => ChannelType.ApiDirect;

    public RestApiChannel(
        ILogger<RestApiChannel> logger,
        HttpClient httpClient,
        string baseUrl,
        string? apiKey,
        string model = "deepseek-chat",
        int maxTokens = 16384)
    {
        _logger = logger;
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
        _maxTokens = maxTokens;

        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    public async Task<AgentResponse> SendAsync(AgentRequest request, CancellationToken ct = default)
    {
        try
        {
            var stepModel = request.Metadata.TryGetValue("model", out var m) ? m : _model;

            var body = new ChatCompletionRequest
            {
                Model = stepModel,
                Messages = [new ChatMessage { Role = "user", Content = request.RenderedPrompt }],
                MaxTokens = _maxTokens,
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_baseUrl}/v1/chat/completions",
                body,
                JsonOptions.Default,
                ct);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(
                JsonOptions.Default,
                ct);

            var content = result?.Choices?.FirstOrDefault()?.Message?.Content ?? "";

            return new AgentResponse
            {
                Content = content,
                Success = !string.IsNullOrWhiteSpace(content),
                Metadata = new()
                {
                    ["model"] = result?.Model ?? stepModel,
                },
            };
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized
            || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogError("Rest API auth error: {Message}", ex.Message);
            return new AgentResponse
            {
                Content = "",
                Success = false,
                ErrorMessage = $"Rest API auth error: {ex.Message}",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("Rest API error: {Message}", ex.Message);
            return new AgentResponse
            {
                Content = "",
                Success = false,
                ErrorMessage = ex.Message,
            };
        }
    }

    private class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = new();

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = false;
    }

    private class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    private class ChatCompletionResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; set; }
    }
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };
}
