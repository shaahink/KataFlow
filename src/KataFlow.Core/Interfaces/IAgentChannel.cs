using KataFlow.Core.Enums;
using KataFlow.Core.Models;

namespace KataFlow.Core.Interfaces;

public interface IAgentChannel
{
    ChannelType Type { get; }

    Task<AgentResponse> SendAsync(
        AgentRequest request,
        CancellationToken ct = default);
}
