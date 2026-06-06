using System.Text.Json;
using KataFlow.Core.Abstractions;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;

namespace KataFlow.Infrastructure;

public class SessionStore : ISessionStore
{
    private readonly IFileSystem _fileSystem;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public SessionStore(IFileSystem fileSystem, string sessionsPath = "./sessions")
    {
        _fileSystem = fileSystem;
    }

    public async Task<Session> CreateAsync(string workflowName, OrchestratorMode mode)
    {
        var session = new Session
        {
            Id = Guid.NewGuid().ToString("N"),
            WorkflowName = workflowName,
            Mode = mode,
        };

        var sessionDir = GetSessionDir(session.Id);
        _fileSystem.CreateDirectory(sessionDir);
        _fileSystem.CreateDirectory(GetArtifactsDir(session.Id));

        await SaveAsync(session);
        return session;
    }

    public async Task<Session?> GetAsync(string sessionId)
    {
        var path = GetSessionFilePath(sessionId);
        if (!_fileSystem.FileExists(path))
            return null;

        var json = await _fileSystem.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<Session>(json, JsonOptions);
    }

    public async Task SaveAsync(Session session)
    {
        var path = GetSessionFilePath(session.Id);
        _fileSystem.CreateDirectory(GetSessionDir(session.Id));

        var json = JsonSerializer.Serialize(session, JsonOptions);
        await _fileSystem.WriteAllTextAsync(path, json);
    }

    public async Task<IReadOnlyList<Session>> ListAsync()
    {
        var sessionsPath = _fileSystem.Combine(_fileSystem.GetCurrentDirectory(), "sessions");
        if (!_fileSystem.DirectoryExists(sessionsPath))
            return [];

        var sessions = new List<Session>();
        foreach (var dir in _fileSystem.GetDirectories(sessionsPath))
        {
            var jsonPath = _fileSystem.Combine(dir, "session.json");
            if (_fileSystem.FileExists(jsonPath))
            {
                var json = await _fileSystem.ReadAllTextAsync(jsonPath);
                var session = JsonSerializer.Deserialize<Session>(json, JsonOptions);
                if (session is not null)
                    sessions.Add(session);
            }
        }
        return sessions.AsReadOnly();
    }

    private string GetSessionDir(string sessionId)
        => _fileSystem.Combine(_fileSystem.GetCurrentDirectory(), "sessions", sessionId);

    private string GetArtifactsDir(string sessionId)
        => _fileSystem.Combine(GetSessionDir(sessionId), "artifacts");

    private string GetSessionFilePath(string sessionId)
        => _fileSystem.Combine(GetSessionDir(sessionId), "session.json");
}
