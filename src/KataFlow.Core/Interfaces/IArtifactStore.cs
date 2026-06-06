using KataFlow.Core.Models;

namespace KataFlow.Core.Interfaces;

public interface IArtifactStore
{
    Task SaveAsync(Session session, string name, string content);
    Task<string?> ReadAsync(Session session, string name);
    string GetPath(Session session, string name);
}
