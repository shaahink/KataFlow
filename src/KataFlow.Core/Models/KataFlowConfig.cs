namespace KataFlow.Core.Models;

public class KataFlowConfig
{
    public string WorkflowsPath { get; set; } = "./workflows";
    public string TemplatesPath { get; set; } = "./templates";
    public string SessionsPath { get; set; } = "./sessions";
    public string DefaultMode { get; set; } = "dev";
}
