using KataFlow.Api;
using KataFlow.Api.Endpoints;
using KataFlow.Core.Abstractions;
using KataFlow.Core.Interfaces;
using KataFlow.ServiceDefaults;
using Microsoft.AspNetCore.SignalR;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
    builder.Configuration.AddJsonFile(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kataflow", "config.json"),
        optional: true,
        reloadOnChange: false);

    builder.Services.AddKataFlowAll(builder.Configuration);

    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

    builder.Services.AddSignalR();

    var app = builder.Build();
    app.UseCors();

    var fs = app.Services.GetRequiredService<IFileSystem>();
    var loader = app.Services.GetRequiredService<IWorkflowLoader>();
    var store = app.Services.GetRequiredService<ISessionStore>();
    var runner = app.Services.GetRequiredService<IWorkflowRunner>();
    var env = app.Services.GetRequiredService<IWebHostEnvironment>();
    var hubContext = app.Services.GetRequiredService<IHubContext<SessionHub>>();

    var root = WorkspaceResolver.ResolveRoot(env.ContentRootPath);
    var cfg = builder.Configuration;
    var wfRel = (cfg.GetSection("KataFlow:WorkflowsPath").Value ?? "workflows").TrimStart('.', '/', '\\');
    var tmplRel = (cfg.GetSection("KataFlow:TemplatesPath").Value ?? "templates").TrimStart('.', '/', '\\');
    var workflowsPath = Path.Combine(root, wfRel);
    var templatesPath = Path.Combine(root, tmplRel);

    app.MapGet("/api/debug/paths", () => Results.Ok(new
    {
        cwd = Directory.GetCurrentDirectory(), root, workflowsPath, templatesPath,
        templatesExist = Directory.Exists(templatesPath),
        templatesFiles = Directory.Exists(templatesPath)
            ? Directory.GetFiles(templatesPath, "*.md", SearchOption.AllDirectories).Length : 0,
    }));

    WorkflowEndpoints.Map(app, fs, loader, workflowsPath);
    TemplateEndpoints.Map(app, fs, templatesPath);
    SessionEndpoints.Map(app, fs, store);
    RunEndpoints.Map(app, loader, runner, store, hubContext);

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
