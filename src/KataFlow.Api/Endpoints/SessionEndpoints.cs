using KataFlow.Core.Abstractions;
using KataFlow.Core.Interfaces;
using KataFlow.Core;

namespace KataFlow.Api.Endpoints;

internal static class SessionEndpoints
{
    public static void Map(IEndpointRouteBuilder app, IFileSystem fs, ISessionStore store)
    {
        app.MapGet("/api/sessions", async () =>
        {
            var sessions = await store.ListAsync();
            return Results.Ok(sessions.Select(s => new
            {
                s.Id, s.WorkflowName, Status = s.Status.ToString(),
                s.CurrentStepIndex, s.CreatedAt, s.CompletedAt
            }));
        });

        app.MapGet("/api/sessions/{id}/artifacts/{name}", async (string id, string name, ISessionStore store, IFileSystem fs) =>
        {
            var session = await store.GetAsync(id);
            if (session is null) return Results.NotFound();
            if (!session.Artifacts.TryGetValue(name, out var path)) return Results.NotFound();
            if (!fs.FileExists(path)) return Results.NotFound();
            var content = await fs.ReadAllTextAsync(path);
            return Results.Ok(new { name, content, path });
        });

        app.MapGet("/api/sessions/{id}", async (string id) =>
        {
            var session = await store.GetAsync(id);
            if (session is null)
                return Results.NotFound(new { error = $"Session '{id}' not found" });

            return Results.Ok(new
            {
                session.Id, session.WorkflowName,
                Status = session.Status.ToString(), Mode = session.Mode.ToString(),
                session.CurrentStepIndex, session.CreatedAt, session.CompletedAt,
                Artifacts = session.Artifacts.Select(a => new { name = a.Key, path = a.Value }),
                Steps = session.History.Select(s => new
                {
                    s.StepName, Status = s.Status.ToString(),
                    s.OutputArtifactPath, s.ErrorMessage, s.StartedAt, s.CompletedAt
                }),
                Budget = new
                {
                    TotalCostUsd = session.TotalCostUsd,
                    TotalInputTokens = session.TotalInputTokens,
                    TotalOutputTokens = session.TotalOutputTokens,
                    Steps = session.Budget.Select(b => new
                    {
                        b.StepName, b.Model, b.InputTokens, b.OutputTokens, b.CostUsd
                    }),
                },
            });
        });

        app.MapPost("/api/sessions/{id}/approve", async (string id, ApproveRequest req) =>
        {
            var sessionDir = fs.Combine(fs.GetCurrentDirectory(), "sessions", id);
            if (!fs.DirectoryExists(sessionDir))
                return Results.NotFound(new { error = $"Session '{id}' not found" });

            var decisionFile = req.Approve
                ? fs.Combine(sessionDir, Constants.ApprovedFile)
                : fs.Combine(sessionDir, Constants.RejectedFile);
            fs.WriteAllText(decisionFile, id);
            return Results.Ok(new { sessionId = id, approved = req.Approve });
        });

        app.MapDelete("/api/sessions/{id}", (string id) =>
        {
            var sessionDir = fs.Combine(fs.GetCurrentDirectory(), "sessions", id);
            if (!fs.DirectoryExists(sessionDir))
                return Results.NotFound(new { error = $"Session '{id}' not found" });
            Directory.Delete(sessionDir, recursive: true);
            return Results.NoContent();
        });

        app.MapDelete("/api/sessions", () =>
        {
            var sessionsDir = fs.Combine(fs.GetCurrentDirectory(), "sessions");
            if (fs.DirectoryExists(sessionsDir))
            {
                foreach (var dir in Directory.GetDirectories(sessionsDir))
                    Directory.Delete(dir, recursive: true);
            }
            return Results.NoContent();
        });
    }
}
