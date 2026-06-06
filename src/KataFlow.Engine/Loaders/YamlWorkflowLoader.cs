using KataFlow.Core.Abstractions;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KataFlow.Engine.Loaders;

public class YamlWorkflowLoader : IWorkflowLoader
{
    private readonly IFileSystem _fileSystem;
    private readonly string _workflowsPath;

    public YamlWorkflowLoader(IFileSystem fileSystem, string workflowsPath = "./workflows")
    {
        _fileSystem = fileSystem;
        _workflowsPath = workflowsPath;
    }

    public WorkflowDefinition Load(string nameOrPath)
    {
        var path = _fileSystem.FileExists(nameOrPath) ? nameOrPath
            : _fileSystem.Combine(_workflowsPath, $"{nameOrPath}.yaml");

        if (!_fileSystem.FileExists(path))
            throw new FileNotFoundException($"Workflow YAML not found: {path}");

        var yaml = _fileSystem.ReadAllTextAsync(path).GetAwaiter().GetResult();
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var raw = deserializer.Deserialize<YamlWorkflowRoot>(yaml);
        return ToWorkflowDefinition(raw.workflow);
    }

    public IReadOnlyList<string> ListAvailable()
    {
        if (!_fileSystem.DirectoryExists(_workflowsPath))
            return [];

        return _fileSystem.GetFiles(_workflowsPath, "*.yaml")
            .Concat(_fileSystem.GetFiles(_workflowsPath, "*.yml"))
            .Select(_fileSystem.GetFileNameWithoutExtension)
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
