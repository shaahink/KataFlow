using System.Text.RegularExpressions;
using KataFlow.Core.Abstractions;

namespace KataFlow.Api.Endpoints;

internal static class TemplateEndpoints
{
    public static void Map(IEndpointRouteBuilder app, IFileSystem fs, string templatesPath)
    {
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
                .Distinct().Order().ToList();

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
    }
}
