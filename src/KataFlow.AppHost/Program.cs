var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.KataFlow_Api>("api")
    .WithEnvironment("ASPNETCORE_URLS", "http://localhost:5100")
    .WithEnvironment("ANTHROPIC_API_KEY", builder.Configuration["ANTHROPIC_API_KEY"] ?? "")
    .WithEnvironment("DEEPSEEK_API_KEY", builder.Configuration["DEEPSEEK_API_KEY"] ?? "")
    .WithEnvironment("OPENAI_API_KEY", builder.Configuration["OPENAI_API_KEY"] ?? "");

builder.AddNpmApp("web", Path.Combine("..", "KataFlow.Web"), "start")
    .WaitFor(api)
    .WithArgs("--port", "4200", "--proxy-config", "proxy.conf.json");

builder.Build().Run();
