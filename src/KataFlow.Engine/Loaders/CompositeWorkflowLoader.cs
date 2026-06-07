using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;

namespace KataFlow.Engine.Loaders;

public class CompositeWorkflowLoader : IWorkflowLoader
{
    private readonly IReadOnlyList<IWorkflowLoader> _loaders;

    public CompositeWorkflowLoader(IEnumerable<IWorkflowLoader> loaders)
    {
        _loaders = loaders.ToList();
    }

    public WorkflowDefinition Load(string nameOrPath)
    {
        foreach (var loader in _loaders)
        {
            if (loader.ListAvailable().Contains(nameOrPath))
                return loader.Load(nameOrPath);
        }
        return _loaders[^1].Load(nameOrPath);
    }

    public IReadOnlyList<string> ListAvailable()
        => _loaders.SelectMany(l => l.ListAvailable()).Distinct().ToList();
}
