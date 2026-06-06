using KataFlow.Core.Abstractions;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;

namespace KataFlow.Infrastructure;

public class ArtifactStore : IArtifactStore
{
    private readonly IFileSystem _fileSystem;
    private readonly string _basePath;

    public ArtifactStore(IFileSystem fileSystem, string sessionsPath = "./sessions")
    {
        _fileSystem = fileSystem;
        _basePath = _fileSystem.Combine(_fileSystem.GetCurrentDirectory(), sessionsPath);
    }

    public async Task SaveAsync(Session session, string name, string content)
    {
        var path = GetPath(session, name);
        _fileSystem.CreateDirectory(GetArtifactsDir(session.Id));
        await _fileSystem.WriteAllTextAsync(path, content);
    }

    public async Task<string?> ReadAsync(Session session, string name)
    {
        var path = GetPath(session, name);
        if (!_fileSystem.FileExists(path))
            return null;
        return await _fileSystem.ReadAllTextAsync(path);
    }

    public string GetPath(Session session, string name)
    {
        return _fileSystem.Combine(_basePath, session.Id, "artifacts", $"{name}.md");
    }

    private string GetArtifactsDir(string sessionId)
        => _fileSystem.Combine(_basePath, sessionId, "artifacts");
}
