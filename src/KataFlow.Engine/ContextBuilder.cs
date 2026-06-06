using System.Collections;
using KataFlow.Core.Abstractions;
using KataFlow.Core.Models;

namespace KataFlow.Engine;

public class ContextBuilder
{
    private readonly IFileSystem _fileSystem;

    public ContextBuilder(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public virtual IReadOnlyDictionary<string, string> Build(Session session, StepDefinition step)
    {
        var vars = new Dictionary<string, string>();

        foreach (var (k, v) in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>())
            vars[k.ToString()!] = v?.ToString() ?? "";

        foreach (var (k, v) in session.Variables)
            vars[k] = v;

        foreach (var artifactName in step.ContextArtifacts)
            if (session.Artifacts.TryGetValue(artifactName, out var path))
                vars[artifactName] = _fileSystem.ReadAllTextAsync(path).GetAwaiter().GetResult();

        vars["_session_id"] = session.Id;
        vars["_step_name"] = step.Name;
        vars["_workflow_name"] = session.WorkflowName;
        vars["_output_path"] = GetOutputPath(session, step);

        return vars;
    }

    private string GetOutputPath(Session session, StepDefinition step)
    {
        var sessionsDir = _fileSystem.Combine(_fileSystem.GetCurrentDirectory(), "sessions", session.Id);
        return _fileSystem.Combine(sessionsDir, $"output-{step.Name}.md");
    }
}
