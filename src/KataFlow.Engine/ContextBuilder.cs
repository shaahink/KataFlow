using System.Collections;
using KataFlow.Core.Models;

namespace KataFlow.Engine;

public class ContextBuilder
{
    public IReadOnlyDictionary<string, string> Build(Session session, StepDefinition step)
    {
        var vars = new Dictionary<string, string>();

        foreach (var (k, v) in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>())
            vars[k.ToString()!] = v?.ToString() ?? "";

        foreach (var (k, v) in session.Variables)
            vars[k] = v;

        foreach (var artifactName in step.ContextArtifacts)
            if (session.Artifacts.TryGetValue(artifactName, out var path))
                vars[artifactName] = File.ReadAllText(path);

        vars["_session_id"] = session.Id;
        vars["_step_name"] = step.Name;
        vars["_workflow_name"] = session.WorkflowName;
        vars["_output_path"] = GetOutputPath(session, step);

        return vars;
    }

    private static string GetOutputPath(Session session, StepDefinition step)
    {
        var sessionsDir = Path.Combine(Directory.GetCurrentDirectory(), "sessions", session.Id);
        return Path.Combine(sessionsDir, $"output-{step.Name}.md");
    }
}
