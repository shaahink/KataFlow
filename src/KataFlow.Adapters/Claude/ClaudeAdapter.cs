using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;

namespace KataFlow.Adapters.Claude;

public class ClaudeAdapter : IAgentAdapter
{
    private readonly Dictionary<ChannelType, IAgentChannel> _channels;

    public string Name => "Claude";
    public AgentType AgentType => AgentType.Claude;
    public IReadOnlyList<ChannelType> SupportedChannels { get; }

    public ClaudeAdapter(IEnumerable<IAgentChannel> channels)
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
