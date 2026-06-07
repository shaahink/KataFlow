var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.KataFlow_Api>("api")
    .WithHttpEndpoint(targetPort: 5100)
    .WithExternalHttpEndpoints();

var web = builder.AddNpmApp("web", Path.Combine("..", "KataFlow.Web"), "start")
    .WithReference(api)
    .WithHttpEndpoint(targetPort: 4200, env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
