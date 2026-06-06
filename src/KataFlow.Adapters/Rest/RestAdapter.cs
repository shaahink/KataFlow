using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;

namespace KataFlow.Adapters.Rest;

public class RestAdapter : IAgentAdapter
{
    private readonly Dictionary<ChannelType, IAgentChannel> _channels;

    public string Name => "Rest";
    public AgentType AgentType => AgentType.Rest;
    public IReadOnlyList<ChannelType> SupportedChannels { get; }

    public RestAdapter(IEnumerable<IAgentChannel> channels)
    {
        _channels = channels.ToDictionary(c => c.Type);
        SupportedChannels = _channels.Keys.ToList().AsReadOnly();
    }

    public Task<AgentResponse> SendAsync(
        AgentRequest request,
        ChannelType channel,
        CancellationToken ct = default)
    {
        if (_channels.TryGetValue(channel, out var ch))
            return ch.SendAsync(request, ct);
        throw new InvalidOperationException(
            $"Channel '{channel}' is not supported by adapter '{Name}'.");
    }
}
