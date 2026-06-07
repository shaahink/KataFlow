var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.KataFlow_Api>("api")
    .WithHttpEndpoint(targetPort: 5100)
    .WithExternalHttpEndpoints();

builder.Build().Run();
