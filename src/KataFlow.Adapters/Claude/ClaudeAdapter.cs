using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;

namespace KataFlow.Adapters.Claude;

public class ClaudeAdapter : IAgentAdapter
{
    private readonly IAgentChannel _fileDrop;
    private readonly IAgentChannel _api;

    public string Name => "Claude";
    public AgentType AgentType => AgentType.Claude;
    public IReadOnlyList<ChannelType> SupportedChannels => [ChannelType.FileDrop, ChannelType.ApiDirect];

    public ClaudeAdapter(
        FileDrop.FileDropChannel fileDrop,
        ClaudeApiChannel api)
    {
        _fileDrop = fileDrop;
        _api = api;
    }

    public Task<AgentResponse> SendAsync(
        AgentRequest request,
        ChannelType channel,
        CancellationToken ct = default)
        => channel == ChannelType.FileDrop
            ? _fileDrop.SendAsync(request, ct)
            : _api.SendAsync(request, ct);
}
