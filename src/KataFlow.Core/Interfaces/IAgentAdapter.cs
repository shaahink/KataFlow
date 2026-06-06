using KataFlow.Core.Enums;
using KataFlow.Core.Models;

namespace KataFlow.Core.Interfaces;

public interface IAgentAdapter
{
    string Name { get; }
    AgentType AgentType { get; }
    IReadOnlyList<ChannelType> SupportedChannels { get; }

    Task<AgentResponse> SendAsync(
        AgentRequest request,
        ChannelType channel,
        CancellationToken ct = default);
}
