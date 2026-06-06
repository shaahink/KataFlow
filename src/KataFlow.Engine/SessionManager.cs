using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;

namespace KataFlow.Engine;

public class SessionManager
{
    private readonly ISessionStore _sessionStore;

    public SessionManager(ISessionStore sessionStore)
    {
        _sessionStore = sessionStore;
    }

    public async Task<Session> ResolveAsync(WorkflowDefinition workflow, SessionContext ctx)
    {
        var session = ctx.SessionId is not null
            ? await _sessionStore.GetAsync(ctx.SessionId)
                ?? throw new InvalidOperationException($"Session not found: {ctx.SessionId}")
            : await _sessionStore.CreateAsync(workflow.Name, ctx.Mode);

        foreach (var (k, v) in ctx.Variables)
            session.Variables[k] = v;

        await PersistAsync(session);
        return session;
    }

    public async Task PersistAsync(Session session)
    {
        await _sessionStore.SaveAsync(session);
    }

    public async Task<SessionResult> FailAsync(Session session, string errorMessage)
    {
        session.Status = SessionStatus.Failed;
        await PersistAsync(session);
        return new SessionResult { SessionId = session.Id, Success = false, ErrorMessage = errorMessage };
    }

    public async Task<SessionResult> CancelAsync(Session session)
    {
        session.Status = SessionStatus.Cancelled;
        await PersistAsync(session);
        return new SessionResult { SessionId = session.Id, Success = false, ErrorMessage = "Cancelled" };
    }

    public async Task<SessionResult> CompleteAsync(Session session)
    {
        session.Status = SessionStatus.Complete;
        session.CompletedAt = DateTimeOffset.UtcNow;
        await PersistAsync(session);
        return new SessionResult { SessionId = session.Id, Success = true };
    }
}
