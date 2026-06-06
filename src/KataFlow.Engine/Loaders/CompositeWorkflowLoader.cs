using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;

namespace KataFlow.Engine.Loaders;

public class CompositeWorkflowLoader : IWorkflowLoader
{
    private readonly PresetWorkflowRegistry _presets;
    private readonly YamlWorkflowLoader _yaml;

    public CompositeWorkflowLoader(PresetWorkflowRegistry presets, YamlWorkflowLoader yaml)
    {
        _presets = presets;
        _yaml = yaml;
    }

    public WorkflowDefinition Load(string nameOrPath)
    {
        if (_presets.ListAvailable().Contains(nameOrPath))
            return _presets.Load(nameOrPath);
        return _yaml.Load(nameOrPath);
    }

    public IReadOnlyList<string> ListAvailable()
        => [.. _presets.ListAvailable(), .. _yaml.ListAvailable()];
}
