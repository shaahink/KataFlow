using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using KataFlow.Engine.Presets;

namespace KataFlow.Engine.Loaders;

public class PresetWorkflowRegistry : IWorkflowLoader
{
    private readonly Dictionary<string, WorkflowDefinition> _presets;

    public PresetWorkflowRegistry()
    {
        _presets = new Dictionary<string, WorkflowDefinition>
        {
            ["software-lifecycle"] = SoftwareLifecyclePreset.Build(),
            ["trading-strategy"] = TradingStrategyPreset.Build(),
            ["review-only"] = ReviewOnlyPreset.Build(),
            ["planner-only"] = PlannerOnlyPreset.Build(),
            ["quick-execute"] = QuickExecutePreset.Build(),
        };
    }

    public WorkflowDefinition Load(string nameOrPath)
    {
        if (_presets.TryGetValue(nameOrPath, out var preset))
            return preset;
        throw new WorkflowNotFoundException(nameOrPath);
    }

    public IReadOnlyList<string> ListAvailable() => [.. _presets.Keys];
}

internal class WorkflowNotFoundException(string name) : Exception($"Workflow not found: {name}")
{
    public string WorkflowName { get; } = name;
}
