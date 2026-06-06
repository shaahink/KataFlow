namespace KataFlow.Core.Interfaces;

public interface IPromptRenderer
{
    string Render(string templatePath, IReadOnlyDictionary<string, string> variables);
}
