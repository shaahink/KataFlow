using System.Text.Json;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;

namespace KataFlow.Infrastructure;

public class SessionStore : ISessionStore
{
    private readonly string _sessionsPath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public SessionStore(string sessionsPath = "./sessions")
    {
        _sessionsPath = Path.GetFullPath(sessionsPath);
        Directory.CreateDirectory(_sessionsPath);
    }

    public async Task<Session> CreateAsync(string workflowName, OrchestratorMode mode)
    {
        var session = new Session
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            WorkflowName = workflowName,
            Mode = mode,
        };

        var sessionDir = GetSessionDir(session.Id);
        Directory.CreateDirectory(sessionDir);
        Directory.CreateDirectory(Path.Combine(sessionDir, "artifacts"));

        await SaveAsync(session);
        return session;
    }

    public async Task<Session?> GetAsync(string sessionId)
    {
        var path = GetSessionFilePath(sessionId);
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<Session>(json, JsonOptions);
    }

    public async Task SaveAsync(Session session)
    {
        var path = GetSessionFilePath(session.Id);
        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(session, JsonOptions);
        await File.WriteAllTextAsync(path, json);
    }

    public async Task<IReadOnlyList<Session>> ListAsync()
    {
        if (!Directory.Exists(_sessionsPath))
            return [];

        var sessions = new List<Session>();
        foreach (var dir in Directory.GetDirectories(_sessionsPath))
        {
            var jsonPath = Path.Combine(dir, "session.json");
            if (File.Exists(jsonPath))
            {
                var json = await File.ReadAllTextAsync(jsonPath);
                var session = JsonSerializer.Deserialize<Session>(json, JsonOptions);
                if (session is not null)
                    sessions.Add(session);
            }
        }
        return sessions.AsReadOnly();
    }

    private string GetSessionDir(string sessionId)
        => Path.Combine(_sessionsPath, sessionId);

    private string GetSessionFilePath(string sessionId)
        => Path.Combine(GetSessionDir(sessionId), "session.json");
}
