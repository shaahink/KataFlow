using KataFlow.Core.Abstractions;
using KataFlow.Core.Interfaces;
using KataFlow.Core;

namespace KataFlow.Api.Endpoints;

internal static class WorkflowEndpoints
{
    public static void Map(IEndpointRouteBuilder app, IFileSystem fs, IWorkflowLoader loader, string workflowsPath)
    {
        app.MapGet("/api/workflows", (ILogger<Program> logger) =>
        {
            var names = loader.ListAvailable();
            var workflows = names.Select(name =>
            {
                try
                {
                    var def = loader.Load(name);
                    return new { name, description = def.Description };
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Failed to load workflow {Name}: {Error}", name, ex.Message);
                    return new { name, description = (string?)null };
                }
            });
            return Results.Ok(workflows);
        });

        app.MapGet("/api/workflows/{name}", async (string name) =>
        {
            try
            {
                var path = fs.FileExists(name) ? name : fs.Combine(workflowsPath, $"{name}.yaml");
                if (!fs.FileExists(path))
                    return Results.NotFound(new { error = $"Workflow '{name}' not found" });
                var yaml = await fs.ReadAllTextAsync(path);
                return Results.Ok(new { name, yaml });
            }
            catch (Exception ex) { return Results.Problem(ex.Message); }
        });

        app.MapPost("/api/workflows", async (CreateWorkflowRequest req) =>
        {
            var path = fs.Combine(workflowsPath, $"{req.Name}.yaml");
            if (fs.FileExists(path))
                return Results.Conflict(new { error = $"Workflow '{req.Name}' already exists" });
            await fs.WriteAllTextAsync(path, req.Yaml);
            return Results.Created($"/api/workflows/{req.Name}", new { name = req.Name });
        });

        app.MapPut("/api/workflows/{name}", async (string name, UpdateWorkflowRequest req) =>
        {
            var path = fs.Combine(workflowsPath, $"{name}.yaml");
            if (!fs.FileExists(path))
                return Results.NotFound(new { error = $"Workflow '{name}' not found" });
            await fs.WriteAllTextAsync(path, req.Yaml);
            return Results.Ok(new { name });
        });

        app.MapDelete("/api/workflows/{name}", (string name) =>
        {
            var path = fs.Combine(workflowsPath, $"{name}.yaml");
            if (!fs.FileExists(path))
                return Results.NotFound(new { error = $"Workflow '{name}' not found" });
            fs.DeleteFile(path);
            return Results.NoContent();
        });
    }
}
