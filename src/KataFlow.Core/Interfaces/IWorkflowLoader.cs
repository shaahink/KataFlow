using KataFlow.Core.Models;

namespace KataFlow.Core.Interfaces;

public interface IWorkflowLoader
{
    WorkflowDefinition Load(string nameOrPath);
    IReadOnlyList<string> ListAvailable();
}
