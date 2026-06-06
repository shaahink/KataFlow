using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;

namespace KataFlow.Infrastructure;

public class ArtifactStore : IArtifactStore
{
    private readonly string _sessionsPath;

    public ArtifactStore(string sessionsPath = "./sessions")
    {
        _sessionsPath = Path.GetFullPath(sessionsPath);
    }

    public async Task SaveAsync(Session session, string name, string content)
    {
        var path = GetPath(session, name);
        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(path, content);
    }

    public Task<string?> ReadAsync(Session session, string name)
    {
        var path = GetPath(session, name);
        if (!File.Exists(path))
            return Task.FromResult<string?>(null);
        return File.ReadAllTextAsync(path).ContinueWith(t => (string?)t.Result);
    }

    public string GetPath(Session session, string name)
    {
        return Path.Combine(_sessionsPath, session.Id, "artifacts", $"{name}.md");
    }
}
