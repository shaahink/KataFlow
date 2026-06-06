using KataFlow.Core.Enums;
using KataFlow.Core.Models;

namespace KataFlow.Core.Interfaces;

public interface ISessionStore
{
    Task<Session> CreateAsync(string workflowName, OrchestratorMode mode);
    Task<Session?> GetAsync(string sessionId);
    Task SaveAsync(Session session);
    Task<IReadOnlyList<Session>> ListAsync();
}
