using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;

namespace KataFlow.Adapters.Rest;

public class RestAdapter : IAgentAdapter
{
    private readonly IAgentChannel _api;

    public string Name => "Rest";
    public AgentType AgentType => AgentType.Rest;
    public IReadOnlyList<ChannelType> SupportedChannels => [ChannelType.ApiDirect];

    public RestAdapter(RestApiChannel api)
    {
        _api = api;
    }

    public Task<AgentResponse> SendAsync(
        AgentRequest request,
        ChannelType channel,
        CancellationToken ct = default)
        => _api.SendAsync(request, ct);
}
