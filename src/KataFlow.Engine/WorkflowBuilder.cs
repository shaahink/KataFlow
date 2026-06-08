using KataFlow.Core.Enums;
using KataFlow.Core.Models;

namespace KataFlow.Engine;

public class WorkflowBuilder
{
    private readonly string _name;
    private string? _description;
    private OrchestratorMode _defaultMode = OrchestratorMode.Dev;
    private readonly List<StepDefinition> _steps = new();
    private readonly Dictionary<string, string> _variables = new();

    private WorkflowBuilder(string name) => _name = name;

    public static WorkflowBuilder Create(string name) => new(name);

    public WorkflowBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public WorkflowBuilder WithDefaultMode(OrchestratorMode mode)
    {
        _defaultMode = mode;
        return this;
    }

    public WorkflowBuilder AddStep(Func<StepBuilder, StepBuilder> configure)
    {
        var stepBuilder = new StepBuilder();
        stepBuilder = configure(stepBuilder);
        _steps.Add(stepBuilder.Build());
        return this;
    }

    public WorkflowBuilder WithVariable(string key, string value)
    {
        _variables[key] = value;
        return this;
    }

    public WorkflowDefinition Build() => new()
    {
        Name = _name,
        Description = _description,
        DefaultMode = _defaultMode,
        Steps = _steps.AsReadOnly(),
        Variables = new Dictionary<string, string>(_variables),
    };

    public class StepBuilder
    {
        private string _name = "";
        private AgentType _agent;
        private string _role = "";
        private string _promptTemplate = "";
        private string? _model;
        private ChannelType? _channelOverride;
        private string? _scriptCommand;
        private ApprovalMode _approval = ApprovalMode.Manual;
        private readonly List<string> _contextArtifacts = new();
        private string? _outputArtifactName;
        private TimeSpan _timeout = TimeSpan.FromMinutes(10);
        private int _maxRetries = 1;

        public StepBuilder Named(string name) { _name = name; return this; }
        public StepBuilder UseAgent(AgentType agent) { _agent = agent; return this; }
        public StepBuilder WithRole(string role) { _role = role; return this; }
        public StepBuilder WithTemplate(string path) { _promptTemplate = path; return this; }
        public StepBuilder WithModel(string model) { _model = model; return this; }
        public StepBuilder ViaFileDrop() { _channelOverride = ChannelType.FileDrop; return this; }
        public StepBuilder ViaApi() { _channelOverride = ChannelType.ApiDirect; return this; }
        public StepBuilder ViaCliExecute() { _channelOverride = ChannelType.CliExecute; return this; }
        public StepBuilder AsScript(string command)
        {
            _agent = AgentType.Script;
            _scriptCommand = command;
            _promptTemplate = "";
            return this;
        }
        public StepBuilder RequireApproval() { _approval = ApprovalMode.Manual; return this; }
        public StepBuilder AutoApprove() { _approval = ApprovalMode.Auto; return this; }
        public StepBuilder WithContext(params string[] artifactNames) { _contextArtifacts.AddRange(artifactNames); return this; }
        public StepBuilder OutputAs(string name) { _outputArtifactName = name; return this; }
        public StepBuilder WithTimeout(TimeSpan timeout) { _timeout = timeout; return this; }
        public StepBuilder WithMaxRetries(int retries) { _maxRetries = retries; return this; }

        public StepDefinition Build() => new()
        {
            Name = _name,
            Agent = _agent,
            Role = _role,
            PromptTemplate = _promptTemplate,
            Model = _model,
            ChannelOverride = _channelOverride,
            Approval = _approval,
            ContextArtifacts = _contextArtifacts.AsReadOnly(),
            OutputArtifactName = _outputArtifactName,
            Timeout = _timeout,
            MaxRetries = _maxRetries,
            ScriptCommand = _scriptCommand,
        };
    }
}
