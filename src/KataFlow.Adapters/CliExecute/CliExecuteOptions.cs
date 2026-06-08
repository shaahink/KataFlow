namespace KataFlow.Adapters.CliExecute;

public class CliExecuteOptions
{
    public string Command { get; set; } = "claude";
    public string Arguments { get; set; } = "--print";
    public CliInputMode InputMode { get; set; } = CliInputMode.Stdin;
    public int TimeoutSeconds { get; set; } = 600;
}

public enum CliInputMode { Stdin, File }
