var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.KataFlow_Api>("api")
    .WithEnvironment("ASPNETCORE_URLS", "http://localhost:5100");

var web = builder.AddNpmApp("web", Path.Combine("..", "KataFlow.Web"), "start")
    .WithReference(api)
    .WithArgs("--port", "4200", "--proxy-config", "proxy.conf.json");

builder.Build().Run();
