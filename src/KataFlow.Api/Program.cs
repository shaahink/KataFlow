using System.Text.RegularExpressions;
using KataFlow.Adapters.Claude;
using KataFlow.Adapters.FileDrop;
using KataFlow.Adapters.Rest;
using KataFlow.Core.Abstractions;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using KataFlow.Engine;
using KataFlow.Engine.Gates;
using KataFlow.Engine.Loaders;
using KataFlow.Infrastructure;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.SignalR;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
    builder.Configuration.AddJsonFile(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kataflow", "config.json"),
        optional: true,
        reloadOnChange: false);

    ConfigureServices(builder.Services);

    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins("http://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));

    builder.Services.AddSignalR();

    var app = builder.Build();
    app.UseCors();
    app.UseRouting();

    MapEndpoints(app);
    app.MapHub<SessionHub>("/hubs/session");

    Console.WriteLine("KataFlow API started");
    await app.RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FATAL: {ex.GetType().Name}: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}

static void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IFileSystem, SystemFileSystem>();
    services.AddSingleton<IPromptRenderer, PromptRenderer>();
    services.AddSingleton<ContextBuilder>();
    services.AddSingleton<StepExecutor>();
    services.AddSingleton<SessionManager>();
    services.AddSingleton<ApprovalFileSignal>();
    services.AddSingleton<PresetWorkflowRegistry>();

    services.AddSingleton<IWorkflowLoader>(sp =>
    {
        var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var fs = sp.GetRequiredService<IFileSystem>();
        var env = sp.GetRequiredService<IWebHostEnvironment>();
        var root = ResolveWorkspaceRoot(env);
        var rel = (config.GetSection("KataFlow:WorkflowsPath").Value ?? "workflows").TrimStart('.', '/', '\\');
        return new CompositeWorkflowLoader([
            sp.GetRequiredService<PresetWorkflowRegistry>(),
            new YamlWorkflowLoader(fs, Path.Combine(root, rel)),
        ]);
    });

    services.AddSingleton<ISessionStore>(sp =>
    {
        var fs = sp.GetRequiredService<IFileSystem>();
        return new SessionStore(fs);
    });
    services.AddSingleton<IArtifactStore>(sp =>
    {
        var fs = sp.GetRequiredService<IFileSystem>();
        return new ArtifactStore(fs);
    });

    services.AddSingleton<FileWatcher>();
    services.AddSingleton<FileDropChannel>(sp =>
    {
        var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        return new FileDropChannel(
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FileDropChannel>>(),
            sp.GetRequiredService<FileWatcher>(),
            sp.GetRequiredService<IPromptRenderer>(),
            sp.GetRequiredService<IFileSystem>(),
            config.GetSection("KataFlow:TemplatesPath").Value ?? "./templates",
            15, 500);
    });

    services.AddOptions<ClaudeOptions>().BindConfiguration("Agents:Claude");
    services.AddHttpClient<ClaudeApiChannel>(client =>
        client.BaseAddress = new Uri("https://api.anthropic.com"));
    services.AddSingleton<IAgentAdapter>(sp =>
        new ClaudeAdapter([
            sp.GetRequiredService<FileDropChannel>(),
            sp.GetRequiredService<ClaudeApiChannel>(),
        ]));

    services.AddOptions<RestOptions>().BindConfiguration("Agents:Rest");
    services.AddHttpClient<RestApiChannel>();
    services.AddSingleton<IAgentAdapter>(sp =>
        new RestAdapter([sp.GetRequiredService<RestApiChannel>()]));

    services.AddSingleton<Func<AgentType, IAgentAdapter>>(sp => agentType => agentType switch
    {
        AgentType.Claude => sp.GetServices<IAgentAdapter>().First(a => a is ClaudeAdapter),
        AgentType.Rest => sp.GetServices<IAgentAdapter>().First(a => a is RestAdapter),
        _ => throw new InvalidOperationException($"Unknown agent type: {agentType}")
    });
    services.AddSingleton<IWorkflowRunner, WorkflowRunner>();
}

static void MapEndpoints(WebApplication app)
{
    var fs = app.Services.GetRequiredService<IFileSystem>();
    var loader = app.Services.GetRequiredService<IWorkflowLoader>();
    var runner = app.Services.GetRequiredService<IWorkflowRunner>();
    var store = app.Services.GetRequiredService<ISessionStore>();
    var config = app.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
    var hubContext = app.Services.GetRequiredService<IHubContext<SessionHub>>();

    var env = app.Services.GetRequiredService<IWebHostEnvironment>();
    var workspaceRoot = ResolveWorkspaceRoot(env);
    var workflowsRel = (config.GetSection("KataFlow:WorkflowsPath").Value ?? "workflows").TrimStart('.', '/', '\\');
    var templatesRel = (config.GetSection("KataFlow:TemplatesPath").Value ?? "templates").TrimStart('.', '/', '\\');
    var workflowsPath = Path.Combine(workspaceRoot, workflowsRel);
    var templatesPath = Path.Combine(workspaceRoot, templatesRel);

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
            var path = fs.FileExists(name)
                ? name
                : fs.Combine(workflowsPath, $"{name}.yaml");
            if (!fs.FileExists(path))
                return Results.NotFound(new { error = $"Workflow '{name}' not found" });

            var yaml = await fs.ReadAllTextAsync(path);
            return Results.Ok(new { name, yaml });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
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

    app.MapGet("/api/debug/paths", () => Results.Ok(new
    {
        cwd = Directory.GetCurrentDirectory(),
        workspaceRoot,
        workflowsPath,
        templatesPath,
        templatesExist = Directory.Exists(templatesPath),
        templatesFiles = Directory.Exists(templatesPath) ? Directory.GetFiles(templatesPath, "*.md", SearchOption.AllDirectories).Length : 0,
    }));

    app.MapGet("/api/templates", () =>
    {
        if (!fs.DirectoryExists(templatesPath))
            return Results.Ok(Array.Empty<string>());

        var files = fs.GetFiles(templatesPath, "*.md")
            .Select(f => Path.GetRelativePath(Directory.GetCurrentDirectory(), f))
            .ToList();
        return Results.Ok(files);
    });

    app.MapGet("/api/templates/{**path}", async (string path) =>
    {
        var fullPath = fs.Combine(Directory.GetCurrentDirectory(), path);
        if (!fs.FileExists(fullPath))
            return Results.NotFound(new { error = $"Template '{path}' not found" });

        var content = await fs.ReadAllTextAsync(fullPath);
        var variables = Regex.Matches(content, @"\{\{(\w+)\}\}")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .Order()
            .ToList();

        return Results.Ok(new { path, content, variables });
    });

    app.MapPut("/api/templates/{**path}", async (string path, UpdateTemplateRequest req) =>
    {
        var fullPath = fs.Combine(Directory.GetCurrentDirectory(), path);
        if (!fs.FileExists(fullPath))
            return Results.NotFound(new { error = $"Template '{path}' not found" });

        await fs.WriteAllTextAsync(fullPath, req.Content);
        return Results.Ok(new { path });
    });

    app.MapGet("/api/sessions", async () =>
    {
        var sessions = await store.ListAsync();
        return Results.Ok(sessions.Select(s => new
        {
            s.Id, s.WorkflowName, Status = s.Status.ToString(),
            s.CurrentStepIndex, s.CreatedAt, s.CompletedAt
        }));
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
            })
        });
    });

    app.MapPost("/api/sessions/{id}/approve", async (string id, ApproveRequest req) =>
    {
        var sessionDir = fs.Combine(fs.GetCurrentDirectory(), "sessions", id);
        if (!fs.DirectoryExists(sessionDir))
            return Results.NotFound(new { error = $"Session '{id}' not found" });

        var decisionFile = req.Approve
            ? fs.Combine(sessionDir, ".approved")
            : fs.Combine(sessionDir, ".rejected");
        fs.WriteAllText(decisionFile, id);
        return Results.Ok(new { sessionId = id, approved = req.Approve });
    });

    app.MapDelete("/api/sessions/{id}", (string id) =>
    {
        var sessionDir = fs.Combine(fs.GetCurrentDirectory(), "sessions", id);
        if (fs.DirectoryExists(sessionDir))
        {
            Directory.Delete(sessionDir, recursive: true);
            return Results.NoContent();
        }
        return Results.NotFound(new { error = $"Session '{id}' not found" });
    });

    app.MapDelete("/api/sessions", () =>
    {
        var sessionsDir = fs.Combine(fs.GetCurrentDirectory(), "sessions");
        if (fs.DirectoryExists(sessionsDir))
        {
            foreach (var dir in Directory.GetDirectories(sessionsDir))
                Directory.Delete(dir, recursive: true);
            return Results.NoContent();
        }
        return Results.NoContent();
    });

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

            var runTask = Task.Run(async () =>
            {
                try
                {
                    var result = await runner.RunAsync(def, new SessionContext
                    {
                        SessionId = session.Id,
                        Mode = ctx.Mode,
                        Variables = ctx.Variables,
                        AutoApprove = ctx.AutoApprove,
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

            // Track for logging, fire-and-forget is intentional for async workflow execution
            _ = runTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Console.Error.WriteLine($"Run task faulted: {t.Exception?.InnerException?.Message}");
            }, TaskContinuationOptions.OnlyOnFaulted);

            return Results.Accepted($"/api/runs/{session.Id}", new { sessionId = session.Id });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    });
}

static string ResolveWorkspaceRoot(IWebHostEnvironment? env = null)
{
    var startDir = env?.ContentRootPath ?? Directory.GetCurrentDirectory();
    var dir = new DirectoryInfo(startDir);
    while (dir is not null)
    {
        if (dir.GetFiles("KataFlow.slnx").Length > 0 || dir.GetFiles("KataFlow.sln").Length > 0)
            return dir.FullName;
        dir = dir.Parent;
    }
    return startDir;
}

public class SessionHub : Hub
{
    public async Task JoinSession(string sessionId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);

    public async Task LeaveSession(string sessionId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
}

public record CreateWorkflowRequest(string Name, string Yaml);
public record UpdateWorkflowRequest(string Yaml);
public record UpdateTemplateRequest(string Content);
public record ApproveRequest(bool Approve);
public record StartRunRequest(string Workflow, Dictionary<string, string>? Variables = null, bool AutoApprove = false);
