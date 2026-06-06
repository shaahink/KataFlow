using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KataFlow.Engine.Loaders;

public class YamlWorkflowLoader : IWorkflowLoader
{
    private readonly string _workflowsPath;

    public YamlWorkflowLoader(string workflowsPath = "./workflows")
    {
        _workflowsPath = workflowsPath;
    }

    public WorkflowDefinition Load(string nameOrPath)
    {
        var path = File.Exists(nameOrPath) ? nameOrPath : Path.Combine(_workflowsPath, $"{nameOrPath}.yaml");

        if (!File.Exists(path))
            throw new FileNotFoundException($"Workflow YAML not found: {path}");

        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var raw = deserializer.Deserialize<YamlWorkflowRoot>(yaml);

        return ToWorkflowDefinition(raw.workflow);
    }

    public IReadOnlyList<string> ListAvailable()
    {
        if (!Directory.Exists(_workflowsPath))
            return [];

        return Directory.GetFiles(_workflowsPath, "*.yaml")
            .Concat(Directory.GetFiles(_workflowsPath, "*.yml"))
            .Select(Path.GetFileNameWithoutExtension)
            .Where(x => x is not null)
            .ToList()!;
    }

    private class YamlWorkflowRoot
    {
        public YamlWorkflow workflow { get; set; } = new();
    }

    private class YamlWorkflow
    {
        public string name { get; set; } = "";
        public string? description { get; set; }
        public string? default_mode { get; set; }
        public Dictionary<string, string>? variables { get; set; }
        public List<YamlStep> steps { get; set; } = new();
    }

    private class YamlStep
    {
        public string name { get; set; } = "";
        public string agent { get; set; } = "";
        public string role { get; set; } = "";
        public string prompt_template { get; set; } = "";
        public string? model { get; set; }
        public string? channel { get; set; }
        public string? approval { get; set; }
        public List<string>? context_artifacts { get; set; }
        public string? output_artifact { get; set; }
        public int? timeout_minutes { get; set; }
        public int? max_retries { get; set; }

        public StepDefinition ToStepDefinition() => new()
        {
            Name = name,
            Agent = agent.ToLowerInvariant() switch
            {
                "claude" => AgentType.Claude,
                "rest" => AgentType.Rest,
                _ => throw new InvalidOperationException($"Unknown agent type: {agent}")
            },
            Role = role,
            PromptTemplate = prompt_template,
            Model = model,
            ChannelOverride = channel?.ToLowerInvariant() switch
            {
                "filedrop" => ChannelType.FileDrop,
                "api" => ChannelType.ApiDirect,
                _ => null
            },
            Approval = approval?.ToLowerInvariant() switch
            {
                "auto" => ApprovalMode.Auto,
                "manual" => ApprovalMode.Manual,
                _ => ApprovalMode.Manual
            },
            ContextArtifacts = context_artifacts ?? [],
            OutputArtifactName = output_artifact,
            Timeout = TimeSpan.FromMinutes(timeout_minutes ?? 10),
            MaxRetries = max_retries ?? 1,
        };
    }

    private static WorkflowDefinition ToWorkflowDefinition(YamlWorkflow yaml)
    {
        return new WorkflowDefinition
        {
            Name = yaml.name,
            Description = yaml.description,
            DefaultMode = yaml.default_mode?.ToLowerInvariant() switch
            {
                "headless" => OrchestratorMode.Headless,
                _ => OrchestratorMode.Dev
            },
            Steps = yaml.steps.Select(s => s.ToStepDefinition()).ToList().AsReadOnly(),
            Variables = yaml.variables ?? new(),
        };
    }
}
