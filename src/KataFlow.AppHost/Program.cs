var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.KataFlow_Api>("api")
    .WithEnvironment("ASPNETCORE_URLS", "http://localhost:5100");

builder.Build().Run();
