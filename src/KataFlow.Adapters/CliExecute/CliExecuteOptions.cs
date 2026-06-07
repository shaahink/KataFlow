namespace KataFlow.Adapters.CliExecute;

public class CliExecuteOptions
{
    public string Command { get; set; } = "opencode";
    public string ArgumentsTemplate { get; set; } = "--prompt \"{input}\"";
    public int TimeoutSeconds { get; set; } = 600;
}
