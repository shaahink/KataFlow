using System.Text.RegularExpressions;
using KataFlow.Core.Abstractions;
using KataFlow.Core.Interfaces;

namespace KataFlow.Engine;

public partial class PromptRenderer : IPromptRenderer
{
    private readonly IFileSystem _fileSystem;

    public PromptRenderer(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string Render(string templatePath, IReadOnlyDictionary<string, string> variables)
    {
        if (!_fileSystem.FileExists(templatePath))
            throw new FileNotFoundException($"Prompt template not found: {templatePath}", templatePath);

        var template = _fileSystem.ReadAllTextAsync(templatePath).GetAwaiter().GetResult();

        return VariableRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (variables.TryGetValue(key, out var value))
                return value;
            throw new InvalidOperationException($"Template variable '{{{{{key}}}}}' is not set. Template: {templatePath}");
        });
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}", RegexOptions.Compiled)]
    private static partial Regex VariableRegex();
}
