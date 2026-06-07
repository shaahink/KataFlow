using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace KataFlow.Api.Endpoints;

internal static class RunEndpoints
{
    public static void Map(IEndpointRouteBuilder app, IWorkflowLoader loader, IWorkflowRunner runner,
        ISessionStore store, IHubContext<Api.SessionHub> hubContext)
    {
        app.MapPost("/api/runs", async (StartRunRequest req) =>
        {
            try
            {
                var def = loader.Load(req.Workflow);
                var ctx = new SessionContext
                {
                    Mode = OrchestratorMode.Dev,
                    Variables = req.Variables ?? new(),
                    AutoApprove = req.AutoApprove,
                };

                var session = await store.CreateAsync(def.Name, OrchestratorMode.Dev);
                foreach (var (k, v) in ctx.Variables)
                    session.Variables[k] = v;
                await store.SaveAsync(session);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await runner.RunAsync(def, new SessionContext
                        {
                            SessionId = session.Id, Mode = ctx.Mode,
                            Variables = ctx.Variables, AutoApprove = ctx.AutoApprove,
                        });
                        await hubContext.Clients.Group(session.Id).SendAsync("SessionCompleted", new
                        {
                            session.Id, result.Success, result.ErrorMessage
                        });
                    }
                    catch (Exception ex)
                    {
                        await hubContext.Clients.Group(session.Id).SendAsync("SessionError", new
                        {
                            session.Id, error = ex.Message
                        });
                    }
                });

                return Results.Accepted($"/api/runs/{session.Id}", new { sessionId = session.Id });
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });
    }
}
