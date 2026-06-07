# KataFlow — Spec & Software Design

> **Status**: Design document v2.0 — ready for implementation
> **Target runtime**: .NET 10 / C# 14
> **Binary name**: `kataflow`
> **Implementation agent**: OpenCode / DeepSeek — single-pass deliverable

---

## 1. Purpose

A .NET 10 CLI application that orchestrates multi-agent AI workflows. The operator defines a pipeline of steps; each step is assigned to an agent (Claude as planner/reviewer, OpenCode/DeepSeek as executor). The orchestrator handles prompt composition, context injection, agent communication via file-drop, approval gating, artifact passing between steps, and session state — removing the manual prompt-management overhead from the current workflow.

**Primary use cases (design must support both):**

1. **Trading strategy lifecycle** — Claude generates a strategy, an executor agent runs tests and produces feedback, Claude reviews and advises, a final agent writes a report for the operator.
2. **Software engineering lifecycle** — plan → implement → review → report, with operator approval gates between stages.

The tool is built as a generic orchestrator; the above are first-class baked-in presets.

---

## 2. Core Concepts

| Concept | Description |
|---|---|
| **Workflow** | A named, ordered sequence of steps. Defined in YAML or as a baked-in C# preset. |
| **Step** | One unit of work: a prompt template + an agent + a channel + an approval mode. |
| **StepType** | `Sequential` (default, v1) or `Parallel` (v2 — fan out N workers). Modelled now; only Sequential is executed in v1. |
| **Agent** | Claude (via Anthropic API) or any OpenAI-compatible REST endpoint (OpenCode/DeepSeek, etc.). |
| **Channel** | How the orchestrator talks to the agent: `FileDrop` (dev mode — file system) or `ApiDirect` (headless mode — REST). |
| **Session** | A single run of a workflow. Has a unique ID, tracks current step, holds all artifacts. Persisted to disk after every step. |
| **Artifact** | Any text output produced by a step. Stored under a name; referenced by later steps via `{{artifact_name}}` in templates. |
| **Approval gate** | A per-step decision point: in-process interactive (operator presses a key while the runner is live) or out-of-band (session pauses, operator runs `kataflow approve`). |
| **Prompt template** | A Markdown file with `{{variable}}` placeholders. The operator's primary prompt-engineering surface — plain files, editable, version-controllable. |
| **WorkflowContext** | File-content variables injected at run time via `--context name=./path`. Content is read from disk and available as `{{name}}` in templates. |

---

## 3. Architecture

```
┌─────────────────────────────────────────────────────┐
│  CLI Layer  (KataFlow.Cli)                          │
│  Commands: run · approve · status · watch · list    │
└────────────────────┬────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────┐
│  Engine  (KataFlow.Engine)                          │
│  WorkflowRunner → StepExecutor → ApprovalGate       │
│  ContextBuilder · PromptRenderer · ArtifactStore    │
└────────────────────┬────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────┐
│  Adapters  (KataFlow.Adapters)                      │
│  ClaudeAdapter    RestAdapter                       │
│  Channel: FileDrop  │  Channel: ApiDirect           │
└─────────────────────────────────────────────────────┘
```

### Communication channels

**FileDrop** (default for `mode: dev`)
The orchestrator writes a rendered Markdown task file into a per-session watched directory. A `FileSystemWatcher` detects the agent's output file. Both Claude Code and OpenCode naturally consume Markdown context files dropped into a directory.

```
sessions/{session-id}/
  task-{step-name}.md       ← orchestrator writes (includes output path instruction)
  output-{step-name}.md     ← agent writes (watched by FileSystemWatcher)
  artifacts/
    plan.md
    implementation.md
    review.md
```

**ApiDirect** (default for `mode: headless`)
The orchestrator calls the agent's REST API directly. Claude's role is filled via the Anthropic Messages API. Any executor role is filled via an OpenAI-compatible endpoint (DeepSeek, OpenCode-hosted, etc.). No file system involvement; fully automatable.

---

## 4. Project Structure

```
KataFlow/
├── KataFlow.sln
├── src/
│   ├── KataFlow.Cli/
│   │   ├── Program.cs                     # Startup, DI, command registration
│   │   ├── Commands/
│   │   │   ├── RunCommand.cs
│   │   │   ├── ApproveCommand.cs
│   │   │   ├── StatusCommand.cs
│   │   │   ├── WatchCommand.cs
│   │   │   └── ListCommand.cs
│   │   └── appsettings.json
│   │
│   ├── KataFlow.Core/
│   │   ├── Models/
│   │   │   ├── WorkflowDefinition.cs
│   │   │   ├── StepDefinition.cs
│   │   │   ├── Session.cs
│   │   │   ├── SessionStep.cs
│   │   │   ├── Artifact.cs
│   │   │   ├── AgentRequest.cs
│   │   │   └── AgentResponse.cs
│   │   ├── Enums/
│   │   │   ├── AgentType.cs
│   │   │   ├── ChannelType.cs
│   │   │   ├── ApprovalMode.cs
│   │   │   ├── SessionStatus.cs
│   │   │   ├── StepType.cs
│   │   │   └── OrchestratorMode.cs
│   │   └── Interfaces/
│   │       ├── IAgentAdapter.cs
│   │       ├── IAgentChannel.cs
│   │       ├── IWorkflowRunner.cs
│   │       ├── IWorkflowLoader.cs
│   │       ├── ISessionStore.cs
│   │       ├── IArtifactStore.cs
│   │       ├── IPromptRenderer.cs
│   │       └── IApprovalGate.cs
│   │
│   ├── KataFlow.Engine/
│   │   ├── WorkflowRunner.cs
│   │   ├── StepExecutor.cs              # single-step execution + retry logic
│   │   ├── ContextBuilder.cs
│   │   ├── PromptRenderer.cs
│   │   ├── Gates/
│   │   │   ├── ManualApprovalGate.cs
│   │   │   └── AutoApprovalGate.cs
│   │   └── Loaders/
│   │       ├── YamlWorkflowLoader.cs
│   │       ├── PresetWorkflowRegistry.cs
│   │       └── CompositeWorkflowLoader.cs
│   │
│   ├── KataFlow.Adapters/
│   │   ├── FileDrop/
│   │   │   ├── FileDropChannel.cs
│   │   │   └── FileDropOptions.cs
│   │   ├── Claude/
│   │   │   ├── ClaudeAdapter.cs
│   │   │   ├── ClaudeApiChannel.cs
│   │   │   └── ClaudeOptions.cs
│   │   └── Rest/
│   │       ├── RestAdapter.cs
│   │       ├── RestApiChannel.cs
│   │       └── RestOptions.cs
│   │
│   └── KataFlow.Infrastructure/
│       ├── SessionStore.cs               # JSON file-backed
│       ├── ArtifactStore.cs
│       ├── FileWatcher.cs
│       └── Logging/
│           └── KataFlowLogger.cs
│
├── workflows/                            # YAML workflow definitions (user-editable)
│   ├── software-lifecycle.yaml
│   ├── trading-strategy.yaml
│   └── review-only.yaml
│
├── templates/                            # Prompt Markdown templates (user-editable)
│   ├── _system/
│   │   └── output-instructions.md        # auto-appended to every task file
│   ├── engineering/
│   │   ├── planner.md
│   │   ├── executor.md
│   │   ├── reviewer.md
│   │   └── reporter.md
│   └── trading/
│       ├── strategy-generator.md
│       ├── strategy-tester.md
│       ├── strategy-reviewer.md
│       └── reporter.md
│
└── sessions/                             # Runtime session data (gitignored)
```

---

## 5. Interfaces

All interfaces live in `KataFlow.Core/Interfaces/`.

### IAgentAdapter

```csharp
public interface IAgentAdapter
{
    string Name { get; }
    AgentType AgentType { get; }
    IReadOnlyList<ChannelType> SupportedChannels { get; }

    Task<AgentResponse> SendAsync(
        AgentRequest request,
        ChannelType channel,
        CancellationToken ct = default);
}
```

### IAgentChannel

```csharp
public interface IAgentChannel
{
    ChannelType Type { get; }

    Task<AgentResponse> SendAsync(
        AgentRequest request,
        CancellationToken ct = default);
}
```

### IWorkflowRunner

```csharp
public interface IWorkflowRunner
{
    Task<SessionResult> RunAsync(
        WorkflowDefinition workflow,
        SessionContext context,
        CancellationToken ct = default);
}
```

### IWorkflowLoader

```csharp
public interface IWorkflowLoader
{
    WorkflowDefinition Load(string nameOrPath);
    IReadOnlyList<string> ListAvailable();
}
```

### IApprovalGate

```csharp
public interface IApprovalGate
{
    Task<ApprovalDecision> RequestApprovalAsync(
        StepResult result,
        CancellationToken ct = default);
}
```

### ISessionStore

```csharp
public interface ISessionStore
{
    Task<Session> CreateAsync(string workflowName, OrchestratorMode mode);
    Task<Session?> GetAsync(string sessionId);
    Task SaveAsync(Session session);
    Task<IReadOnlyList<Session>> ListAsync();
}
```

### IArtifactStore

```csharp
public interface IArtifactStore
{
    Task SaveAsync(Session session, string name, string content);
    Task<string?> ReadAsync(Session session, string name);
    string GetPath(Session session, string name);
}
```

### IPromptRenderer

```csharp
public interface IPromptRenderer
{
    string Render(string templatePath, IReadOnlyDictionary<string, string> variables);
}
```

---

## 6. Domain Models

### WorkflowDefinition

```csharp
public record WorkflowDefinition
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public OrchestratorMode DefaultMode { get; init; } = OrchestratorMode.Dev;
    public required IReadOnlyList<StepDefinition> Steps { get; init; }
    public Dictionary<string, string> Variables { get; init; } = new();
}
```

### StepDefinition

```csharp
public record StepDefinition
{
    public required string Name { get; init; }
    public required AgentType Agent { get; init; }
    public required string Role { get; init; }              // "planner" | "executor" | "reviewer" | "reporter" | custom
    public required string PromptTemplate { get; init; }    // path to .md template
    public string? Model { get; init; }                     // overrides agent-level default; null = use agent default
    public ChannelType? ChannelOverride { get; init; }      // null = derive from session mode
    public ApprovalMode Approval { get; init; } = ApprovalMode.Manual;
    public IReadOnlyList<string> ContextArtifacts { get; init; } = [];  // artifact names from prior steps
    public string? OutputArtifactName { get; init; }        // name under which to store this step's output
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(10);
    public int MaxRetries { get; init; } = 1;

    // v2 parallel workers — modelled now, Sequential-only in v1 execution
    public StepType Type { get; init; } = StepType.Sequential;
    public IReadOnlyList<string> DependsOn { get; init; } = [];   // DAG edges (v2)
    public int WorkerCount { get; init; } = 1;                     // >1 = parallel fan-out (v2)
    public bool UseWorktree { get; init; } = false;                // git worktree isolation per worker (v2)
}
```

### Session

```csharp
// Class (not record) — mutable aggregate, persisted after every step
public class Session
{
    public required string Id { get; init; }
    public required string WorkflowName { get; init; }
    public OrchestratorMode Mode { get; init; }
    public SessionStatus Status { get; set; } = SessionStatus.Running;
    public int CurrentStepIndex { get; set; }
    public Dictionary<string, string> Artifacts { get; } = new();   // name → file path
    public Dictionary<string, string> Variables { get; } = new();   // workflow + session + context vars merged
    public List<SessionStep> History { get; } = new();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
```

### SessionStep

```csharp
public class SessionStep
{
    public required string StepName { get; init; }
    public SessionStatus Status { get; set; }
    public string? OutputArtifactPath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
```

### AgentRequest / AgentResponse

```csharp
public record AgentRequest
{
    public required string SessionId { get; init; }
    public required string StepName { get; init; }
    public required string RenderedPrompt { get; init; }            // fully resolved markdown, ready to send
    public IReadOnlyDictionary<string, string> ContextFiles { get; init; } = new Dictionary<string, string>();
    // name → content (already read from disk; channels may inline or pass as separate messages)
    public Dictionary<string, string> Metadata { get; init; } = new();
}

public record AgentResponse
{
    public required string Content { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();  // token usage, model used, etc.
}
```

### SessionResult / StepResult

```csharp
public record SessionResult
{
    public required string SessionId { get; init; }
    public bool Success { get; init; }
    public IReadOnlyList<StepResult> Steps { get; init; } = [];
    public string? ErrorMessage { get; init; }
}

public record StepResult
{
    public required string StepName { get; init; }
    public bool Success { get; init; }
    public string? ArtifactPath { get; init; }
    public string? ArtifactContent { get; init; }     // populated for approval gate preview
    public string? ErrorMessage { get; init; }
}
```

---

## 7. Enums

```csharp
public enum AgentType      { Claude, Rest }
// Claude = Anthropic API. Rest = any OpenAI-compatible endpoint (DeepSeek, OpenCode, local).
// A step's model field selects the specific model within the agent type.

public enum ChannelType    { FileDrop, ApiDirect }
public enum ApprovalMode   { Manual, Auto }
public enum OrchestratorMode { Dev, Headless }
public enum SessionStatus  { Running, WaitingApproval, Complete, Failed, Cancelled }
public enum StepType       { Sequential, Parallel }   // Parallel = v2; v1 ignores Parallel steps gracefully
public enum ApprovalDecision { Approve, Reject }
```

---

## 8. YAML Workflow Format

### software-lifecycle.yaml

```yaml
workflow:
  name: software-lifecycle
  description: "Plan → implement → review → report cycle for feature development"
  default_mode: dev

  variables:
    project: ""
    output_dir: "./src"

  steps:
    - name: plan
      agent: claude
      role: planner
      prompt_template: templates/engineering/planner.md
      approval: manual
      output_artifact: plan
      timeout_minutes: 15

    - name: implement
      agent: rest
      role: executor
      prompt_template: templates/engineering/executor.md
      context_artifacts: [plan]
      approval: auto
      output_artifact: implementation
      timeout_minutes: 30

    - name: review
      agent: claude
      role: reviewer
      prompt_template: templates/engineering/reviewer.md
      context_artifacts: [plan, implementation]
      approval: manual
      output_artifact: review
      timeout_minutes: 10

    - name: report
      agent: claude
      role: reporter
      prompt_template: templates/engineering/reporter.md
      context_artifacts: [plan, implementation, review]
      approval: auto
      output_artifact: report
      timeout_minutes: 10
```

### trading-strategy.yaml

```yaml
workflow:
  name: trading-strategy
  description: "Generate, test, review, and report on a trading strategy"
  default_mode: dev

  variables:
    asset: "BTCUSDT"
    timeframe: "1h"
    max_drawdown: "10%"
    max_leverage: "3x"

  steps:
    - name: generate-strategy
      agent: claude
      role: planner
      prompt_template: templates/trading/strategy-generator.md
      approval: manual
      output_artifact: strategy
      timeout_minutes: 15

    - name: test-strategy
      agent: rest
      role: executor
      prompt_template: templates/trading/strategy-tester.md
      context_artifacts: [strategy]
      approval: auto
      output_artifact: test-results
      timeout_minutes: 45

    - name: review-strategy
      agent: claude
      role: reviewer
      prompt_template: templates/trading/strategy-reviewer.md
      context_artifacts: [strategy, test-results]
      approval: manual
      output_artifact: review
      timeout_minutes: 10

    - name: write-report
      agent: claude
      role: reporter
      prompt_template: templates/trading/reporter.md
      context_artifacts: [strategy, test-results, review]
      approval: auto
      output_artifact: report
      timeout_minutes: 10
```

### review-only.yaml

```yaml
workflow:
  name: review-only
  description: "Review an existing artifact (pass via --context implementation=./path)"
  default_mode: headless

  steps:
    - name: review
      agent: claude
      role: reviewer
      prompt_template: templates/engineering/reviewer.md
      context_artifacts: [implementation]
      approval: manual
      output_artifact: review
      timeout_minutes: 10
```

---

## 9. C# Fluent Preset Builder

Baked-in presets live in `KataFlow.Engine/Loaders/PresetWorkflowRegistry.cs`. The fluent builder is the canonical way to define them.

```csharp
// KataFlow.Engine/Loaders/Presets/SoftwareLifecyclePreset.cs
internal static class SoftwareLifecyclePreset
{
    public static WorkflowDefinition Build() => WorkflowBuilder
        .Create("software-lifecycle")
        .WithDescription("Plan → implement → review → report cycle")
        .WithDefaultMode(OrchestratorMode.Dev)
        .AddStep(s => s
            .Named("plan")
            .UseAgent(AgentType.Claude)
            .WithRole("planner")
            .WithTemplate("templates/engineering/planner.md")
            .ViaFileDrop()
            .RequireApproval()
            .OutputAs("plan")
            .WithTimeout(TimeSpan.FromMinutes(15)))
        .AddStep(s => s
            .Named("implement")
            .UseAgent(AgentType.Rest)
            .WithRole("executor")
            .WithTemplate("templates/engineering/executor.md")
            .WithContext("plan")
            .ViaFileDrop()
            .AutoApprove()
            .OutputAs("implementation")
            .WithTimeout(TimeSpan.FromMinutes(30)))
        .AddStep(s => s
            .Named("review")
            .UseAgent(AgentType.Claude)
            .WithRole("reviewer")
            .WithTemplate("templates/engineering/reviewer.md")
            .WithContext("plan", "implementation")
            .ViaFileDrop()
            .RequireApproval()
            .OutputAs("review")
            .WithTimeout(TimeSpan.FromMinutes(10)))
        .AddStep(s => s
            .Named("report")
            .UseAgent(AgentType.Claude)
            .WithRole("reporter")
            .WithTemplate("templates/engineering/reporter.md")
            .WithContext("plan", "implementation", "review")
            .ViaFileDrop()
            .AutoApprove()
            .OutputAs("report")
            .WithTimeout(TimeSpan.FromMinutes(10)))
        .Build();
}
```

`PresetWorkflowRegistry` aggregates all presets and implements `IWorkflowLoader`:

```csharp
public class PresetWorkflowRegistry : IWorkflowLoader
{
    private readonly Dictionary<string, WorkflowDefinition> _presets;

    public PresetWorkflowRegistry()
    {
        _presets = new Dictionary<string, WorkflowDefinition>
        {
            ["software-lifecycle"]  = SoftwareLifecyclePreset.Build(),
            ["trading-strategy"]    = TradingStrategyPreset.Build(),
            ["review-only"]         = ReviewOnlyPreset.Build(),
            ["planner-only"]        = PlannerOnlyPreset.Build(),
            ["quick-execute"]       = QuickExecutePreset.Build(),
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
```

`CompositeWorkflowLoader` tries presets first, then YAML files:

```csharp
public class CompositeWorkflowLoader : IWorkflowLoader
{
    private readonly PresetWorkflowRegistry _presets;
    private readonly YamlWorkflowLoader _yaml;

    public WorkflowDefinition Load(string nameOrPath)
    {
        if (_presets.ListAvailable().Contains(nameOrPath))
            return _presets.Load(nameOrPath);
        return _yaml.Load(nameOrPath);  // treats nameOrPath as a file path
    }

    public IReadOnlyList<string> ListAvailable()
        => [.. _presets.ListAvailable(), .. _yaml.ListAvailable()];
}
```

---

## 10. Prompt Template Format

Templates are Markdown files in the `templates/` directory. They are the operator's primary prompt-engineering surface — edit them freely, commit them, share them.

### Variable resolution order (highest priority first)

1. Reserved system variables (always available, injected automatically)
2. Context artifacts from prior steps (`{{plan}}`, `{{test-results}}`, etc.)
3. Session variables (set via `--var key=value` at run time)
4. Workflow-level variables (defined in YAML `variables:` block)
5. Environment variables

### Reserved system variables (auto-injected, read-only)

| Variable | Value |
|---|---|
| `{{_output_path}}` | Absolute path where the agent **must** write its response |
| `{{_session_id}}` | Current session ID |
| `{{_step_name}}` | Current step name |
| `{{_workflow_name}}` | Current workflow name |

### Template example — `templates/engineering/planner.md`

```markdown
# Task: Plan

## Project
{{project}}

## Goal
{{goal}}

## Constraints
{{constraints}}

## Instructions
You are a senior software architect. Produce a detailed implementation plan including:
- Component breakdown
- Edge cases to consider
- Suggested file structure
- Open questions

Return a single Markdown document starting with `# Plan`.
```

### Template example — `templates/engineering/executor.md`

```markdown
# Task: Implement

## Plan
{{plan}}

## Instructions
You are a senior .NET developer. Implement the plan above.
- Follow the component breakdown exactly
- Write idiomatic C# 14 / .NET 10
- One file per `## File: path/to/file.cs` section

Return the full implementation in a single Markdown document.
```

### Template example — `templates/trading/strategy-generator.md`

```markdown
# Task: Generate Trading Strategy

## Asset / Timeframe
Asset: {{asset}}
Timeframe: {{timeframe}}

## Constraints
Max drawdown: {{max_drawdown}}
Max leverage: {{max_leverage}}

## Domain Context
{{rules}}

## Codebase Context
{{codebase}}

## Instructions
You are a quantitative trading strategist. Design a complete, backtestable trading strategy.
Include: entry/exit logic, position sizing, risk management, parameter values, and pseudocode.

Return a single Markdown document starting with `# Strategy`.
```

### Output instructions system partial — `templates/_system/output-instructions.md`

This file is **automatically appended** to every rendered task file by `FileDropChannel`. It tells the agent exactly where to write its response.

```markdown
---

## Output Instructions

Write your complete response as a Markdown document to the following path:

```
{{_output_path}}
```

- Write the **entire** response to that file. Do not write to any other location.
- The orchestrator is watching for that exact file. Once created, the step completes.
- Do not stream partial output — write the final complete document in one operation.
```

---

## 11. Context Injection

The operator injects file content into a workflow at run time using `--context`. The content is read from disk, stored in the session's `Variables` dictionary, and available as `{{name}}` in all templates for that run.

```bash
kataflow run --workflow trading-strategy \
  --context rules=./trading-rules.md \
  --context codebase=./engine-summary.md \
  --var asset=ETHUSDT
```

`ContextBuilder` merges variables in resolution-order priority (§10) before passing them to `PromptRenderer`.

---

## 12. FileDrop Channel — Detailed Flow

```
Orchestrator                           Agent (Claude Code / OpenCode)
──────────                             ──────────────────────────────
Write task file ──────────────────►  sessions/{id}/task-{step}.md
                                       (agent reads file, works...)

FSW event ◄────────────────────────  sessions/{id}/output-{step}.md
Read output
Validate (non-empty)
Store artifact
Proceed to approval gate
```

**`FileDropChannel.cs` responsibilities:**
1. Auto-append `templates/_system/output-instructions.md` (with `{{_output_path}}` resolved) to the rendered prompt
2. Write the final prompt to `sessions/{id}/task-{step}.md`
3. Register a `FileSystemWatcher` on `sessions/{id}/` watching for `output-{step}.md`
4. Wait with configurable timeout (default 10 min); poll fallback every `PollIntervalMs` if FSW misses events
5. On file created: read, validate non-empty, return `AgentResponse { Success = true, Content = ... }`
6. On timeout: throw `StepTimeoutException` — `StepExecutor` handles retry

**Directory layout at runtime:**

```
{workspace}/
  sessions/
    {session-id}/
      session.json           # serialised Session object
      task-plan.md           # written by orchestrator
      output-plan.md         # written by agent
      task-implement.md
      output-implement.md
      artifacts/
        plan.md              # copied from output-plan.md on step success
        implementation.md
        review.md
        report.md
```

---

## 13. ApiDirect Channel

Used in `headless` mode or when `channel: api` is set on a step.

**Claude role → Anthropic Messages API**

```
POST https://api.anthropic.com/v1/messages
{
  "model": "claude-sonnet-4-6",      // configurable; default from ClaudeOptions.Model
  "max_tokens": 16384,
  "system": "You are a helpful assistant.",
  "messages": [{ "role": "user", "content": "<rendered_prompt>" }]
}
```

**Rest role → OpenAI-compatible API (DeepSeek / OpenCode / any)**

```
POST {RestOptions.BaseUrl}/v1/chat/completions
{
  "model": "deepseek-chat",          // configurable; default from RestOptions.Model
  "messages": [{ "role": "user", "content": "<rendered_prompt>" }],
  "stream": false
}
```

Both channels implement `IAgentChannel`. Adapters select the channel at runtime:

```csharp
public class ClaudeAdapter : IAgentAdapter
{
    private readonly IAgentChannel _fileDrop;
    private readonly IAgentChannel _api;

    public Task<AgentResponse> SendAsync(AgentRequest req, ChannelType channel, CancellationToken ct)
        => channel == ChannelType.FileDrop
            ? _fileDrop.SendAsync(req, ct)
            : _api.SendAsync(req, ct);
}
```

---

## 14. Context Builder

`ContextBuilder.Build(session, step)` produces the flat `Dictionary<string, string>` passed to `PromptRenderer`. Merge order (later entries win):

```csharp
public IReadOnlyDictionary<string, string> Build(Session session, StepDefinition step)
{
    var vars = new Dictionary<string, string>();

    // 4. Env vars
    foreach (var (k, v) in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>())
        vars[k.ToString()!] = v?.ToString() ?? "";

    // 3. Workflow + session variables (includes --var and --context injections)
    foreach (var (k, v) in session.Variables)
        vars[k] = v;

    // 2. Context artifacts from prior steps
    foreach (var artifactName in step.ContextArtifacts)
        if (session.Artifacts.TryGetValue(artifactName, out var path))
            vars[artifactName] = File.ReadAllText(path);

    // 1. Reserved system variables (always highest priority)
    vars["_session_id"]    = session.Id;
    vars["_step_name"]     = step.Name;
    vars["_workflow_name"] = session.WorkflowName;
    vars["_output_path"]   = GetOutputPath(session, step);

    return vars;
}
```

---

## 15. Workflow Runner & Step Executor

### WorkflowRunner

`WorkflowRunner` owns the session lifecycle and loops through steps. It delegates single-step execution to `StepExecutor`.

```csharp
public async Task<SessionResult> RunAsync(WorkflowDefinition workflow, SessionContext ctx, CancellationToken ct)
{
    var session = ctx.SessionId is not null
        ? await _sessionStore.GetAsync(ctx.SessionId) ?? throw new SessionNotFoundException(ctx.SessionId)
        : await _sessionStore.CreateAsync(workflow.Name, ctx.Mode);

    // Merge run-time variables (--var, --context) into session
    foreach (var (k, v) in ctx.Variables)
        session.Variables[k] = v;

    await _sessionStore.SaveAsync(session);

    foreach (var step in workflow.Steps.Skip(session.CurrentStepIndex))
    {
        if (step.Type == StepType.Parallel)
        {
            // v2: fan out workers; v1: skip with warning
            _logger.LogWarning("Parallel steps not supported in v1, skipping {Step}", step.Name);
            continue;
        }

        var stepResult = await _stepExecutor.ExecuteAsync(session, step, ct);

        if (!stepResult.Success)
        {
            session.Status = SessionStatus.Failed;
            await _sessionStore.SaveAsync(session);
            return new SessionResult { SessionId = session.Id, Success = false, ErrorMessage = stepResult.ErrorMessage };
        }

        // Approval gate
        var gate = step.Approval == ApprovalMode.Auto ? _autoGate : _manualGate;
        var decision = await gate.RequestApprovalAsync(stepResult, ct);

        if (decision == ApprovalDecision.Reject)
        {
            session.Status = SessionStatus.Failed;
            await _sessionStore.SaveAsync(session);
            return new SessionResult { SessionId = session.Id, Success = false, ErrorMessage = "Rejected by operator" };
        }

        session.CurrentStepIndex++;
        await _sessionStore.SaveAsync(session);
    }

    session.Status = SessionStatus.Complete;
    session.CompletedAt = DateTimeOffset.UtcNow;
    await _sessionStore.SaveAsync(session);
    return new SessionResult { SessionId = session.Id, Success = true };
}
```

### StepExecutor

`StepExecutor` handles a single step including retry.

```csharp
public async Task<StepResult> ExecuteAsync(Session session, StepDefinition step, CancellationToken ct)
{
    var attempt = 0;
    while (true)
    {
        attempt++;
        try
        {
            // Build context and render prompt
            var vars   = _contextBuilder.Build(session, step);
            var prompt = _promptRenderer.Render(step.PromptTemplate, vars);

            // Resolve adapter and channel
            var adapter = _adapterResolver.Resolve(step.Agent);
            var channel = ResolveChannel(step, session.Mode);
            ValidateChannel(adapter, channel, step);

            // Override model if step specifies one
            var request = new AgentRequest
            {
                SessionId     = session.Id,
                StepName      = step.Name,
                RenderedPrompt = prompt,
                Metadata      = step.Model is not null ? new() { ["model"] = step.Model } : new(),
            };

            var response = await adapter.SendAsync(request, channel, ct);

            if (!response.Success || string.IsNullOrWhiteSpace(response.Content))
                throw new AgentResponseException(response.ErrorMessage ?? "Empty response");

            // Store artifact
            if (step.OutputArtifactName is not null)
            {
                await _artifactStore.SaveAsync(session, step.OutputArtifactName, response.Content);
                session.Artifacts[step.OutputArtifactName] = _artifactStore.GetPath(session, step.OutputArtifactName);
            }

            var sessionStep = new SessionStep
            {
                StepName          = step.Name,
                Status            = SessionStatus.Complete,
                OutputArtifactPath = step.OutputArtifactName is not null
                    ? session.Artifacts[step.OutputArtifactName] : null,
                CompletedAt       = DateTimeOffset.UtcNow,
            };
            session.History.Add(sessionStep);

            return new StepResult
            {
                StepName        = step.Name,
                Success         = true,
                ArtifactPath    = sessionStep.OutputArtifactPath,
                ArtifactContent = response.Content,
            };
        }
        catch (Exception ex) when (attempt <= step.MaxRetries)
        {
            _logger.LogWarning("Step {Step} attempt {Attempt} failed: {Error}. Retrying…", step.Name, attempt, ex.Message);
        }
        catch (Exception ex)
        {
            session.History.Add(new SessionStep
            {
                StepName     = step.Name,
                Status       = SessionStatus.Failed,
                ErrorMessage = ex.Message,
                CompletedAt  = DateTimeOffset.UtcNow,
            });
            return new StepResult { StepName = step.Name, Success = false, ErrorMessage = ex.Message };
        }
    }
}

private static ChannelType ResolveChannel(StepDefinition step, OrchestratorMode mode)
    => step.ChannelOverride ?? (mode == OrchestratorMode.Dev ? ChannelType.FileDrop : ChannelType.ApiDirect);

private static void ValidateChannel(IAgentAdapter adapter, ChannelType channel, StepDefinition step)
{
    if (!adapter.SupportedChannels.Contains(channel))
        throw new InvalidOperationException(
            $"Adapter '{adapter.Name}' does not support channel '{channel}' (step: {step.Name}).");
}
```

---

## 16. Approval Gate

### In-process mode (runner is live)

When the `WorkflowRunner` is executing and reaches a `Manual` step, `ManualApprovalGate.RequestApprovalAsync` blocks and renders the interactive UI:

```
╔══════════════════════════════════════════════════════════╗
║  Step: plan  │  Agent: claude  │  Session: abc-123       ║
╠══════════════════════════════════════════════════════════╣
║  Artifact saved → sessions/abc-123/artifacts/plan.md     ║
║                                                          ║
║  Preview (first 20 lines):                               ║
║  ──────────────────────────────────────────────────────  ║
║  # Plan                                                  ║
║  ## Component breakdown                                  ║
║  ...                                                     ║
║                                                          ║
║  [A] Approve and continue                                ║
║  [R] Reject and stop                                     ║
║  [V] View full artifact                                  ║
║  [E] Edit artifact before continuing                     ║
╚══════════════════════════════════════════════════════════╝
Choice:
```

`[E]` opens the artifact in `$EDITOR`; re-reads on save.

### Out-of-band mode (process exited or backgrounded)

When the runner exits with `session.Status = WaitingApproval`, a decision file is written at `sessions/{id}/.pending-approval`. The operator resumes via:

```bash
kataflow approve --session abc-123          # writes sessions/abc-123/.approved
kataflow approve --session abc-123 --reject # writes sessions/abc-123/.rejected
kataflow run --session abc-123              # resumes; runner reads the decision file
```

`ManualApprovalGate` checks for `.approved` / `.rejected` files before blocking on interactive input. If a decision file exists, it returns immediately and deletes the file.

### Auto gate

Logs `AUTO-APPROVED step:plan session:abc-123` and returns `ApprovalDecision.Approve` immediately.

---

## 17. CLI Commands

Use `System.CommandLine` (2.x / 4.0 preview as available for .NET 10).

```
kataflow run
  --workflow <name|path>    Workflow name (preset or path to YAML). Required unless --session.
  --session <id>            Resume a paused session. Mutually exclusive with --workflow.
  --mode <dev|headless>     Override workflow default mode.
  --auto-approve            Force auto-approve for all steps this run.
  --var key=value           Set/override a session variable (repeatable).
  --context name=./path     Inject file content as a named variable (repeatable).

kataflow approve
  --session <id>            Required.
  --reject                  Reject instead of approve (stops session on next resume).

kataflow status
  --session <id>            Show status of a specific session.
  (no args)                 List all sessions with status summary table.

kataflow list
  workflows                 List available workflows (presets + YAML files in ./workflows/).
  sessions                  List all sessions.

kataflow watch
  --session <id>            Tail the orchestration log for a running session.
```

**Example invocations:**

```bash
# Software engineering run (interactive, dev mode)
kataflow run --workflow software-lifecycle --var project=MyFeature --var goal="Add OAuth login"

# Trading strategy run with domain context injected
kataflow run --workflow trading-strategy \
  --context rules=./trading-rules.md \
  --context codebase=./engine-summary.md \
  --var asset=ETHUSDT

# Headless CI run, all steps auto-approved
kataflow run --workflow software-lifecycle --mode headless --auto-approve

# Resume after out-of-band approval
kataflow approve --session abc-123
kataflow run --session abc-123

# Review only — inject an existing implementation
kataflow run --workflow review-only --context implementation=./src/MyFeature.cs
```

---

## 18. Configuration

`appsettings.json` (in `KataFlow.Cli/`) and user-level override at `~/.kataflow/config.json`.

```json
{
  "KataFlow": {
    "WorkflowsPath": "./workflows",
    "TemplatesPath": "./templates",
    "SessionsPath":  "./sessions",
    "DefaultMode":   "dev"
  },
  "Agents": {
    "Claude": {
      "ApiKey":    "",
      "Model":     "claude-sonnet-4-6",
      "MaxTokens": 16384,
      "FileDrop": {
        "WatchTimeoutMinutes": 15,
        "PollIntervalMs":      500
      }
    },
    "Rest": {
      "ApiKey":    "",
      "BaseUrl":   "https://api.deepseek.com",
      "Model":     "deepseek-chat",
      "MaxTokens": 16384,
      "FileDrop": {
        "WatchTimeoutMinutes": 30,
        "PollIntervalMs":      500
      }
    }
  }
}
```

**API keys** are never stored in source-controlled config. Resolution order:
1. `ANTHROPIC_API_KEY` / `DEEPSEEK_API_KEY` environment variables
2. `.env` file at workspace root
3. `~/.kataflow/config.json` (user-level, gitignored)

**User-level config** at `~/.kataflow/config.json` merges on top of `appsettings.json`. Keys are identical; any key present in the user file overrides the project default.

---

## 19. Key Packages

| Package | Purpose |
|---|---|
| `System.CommandLine` | CLI command parsing |
| `YamlDotNet` | YAML workflow loading |
| `Anthropic.SDK` | Anthropic Messages API (fallback to raw `HttpClient` if SDK lacks a feature) |
| `Microsoft.Extensions.Hosting` | DI, configuration, logging |
| `Microsoft.Extensions.Options` | Typed configuration binding |
| `Spectre.Console` | Terminal UI: approval gate, status tables, progress |
| `Serilog` | Structured logging (console + file sink) |

---

## 20. Error Handling

| Condition | Behaviour |
|---|---|
| Agent returns empty / whitespace response | Retry up to `MaxRetries`, then `StepResult { Success = false }` |
| FileDrop timeout | Retry up to `MaxRetries`, then `StepTimeoutException` → step fails |
| API auth error (401/403) | Fail immediately with clear message; no retry |
| Template variable unresolved | Fail immediately; error names the missing variable |
| Operator rejects step | Session → `Failed`; artifacts retained; operator can inspect |
| YAML parse error | Fail at load time with line/column and message |
| Channel not supported by adapter | Fail immediately at step start (validated before sending) |
| `StepType.Parallel` in v1 | Log warning and skip; session continues |

All session state is persisted to disk after every step. Sessions can be resumed after crashes or intentional interruption.

---

## 21. Baked-in Preset Workflows

| Name | Steps | Description |
|---|---|---|
| `software-lifecycle` | plan → implement → review → report | Full SE lifecycle |
| `trading-strategy` | generate-strategy → test-strategy → review-strategy → write-report | Strategy development cycle |
| `planner-only` | plan | Planning only |
| `review-only` | review | Review an injected artifact |
| `quick-execute` | implement | Single-step executor handoff |

---

## 22. Extension Points

- **New agent type**: implement `IAgentAdapter` + `IAgentChannel` implementations + register in DI.
- **New channel type**: implement `IAgentChannel` + add `ChannelType` enum value + register in DI.
- **New workflow**: drop a YAML file in `workflows/` or add a preset class + register in `PresetWorkflowRegistry`.
- **New approval mode**: implement `IApprovalGate` + extend `ApprovalMode` enum + wire in DI.
- **New variable source**: add a resolution layer to `ContextBuilder.Build()`.

---

## 23. Implementation Order

Work in this sequence so each layer is testable before the next depends on it.

1. **`KataFlow.Core`** — models, enums, interfaces only. No logic. No dependencies.
2. **`KataFlow.Engine`**
   - `PromptRenderer` first (pure string substitution, easiest to unit test)
   - `ContextBuilder` (pure function, unit test with sample sessions)
   - `WorkflowBuilder` fluent API + `PresetWorkflowRegistry`
   - `YamlWorkflowLoader` + `CompositeWorkflowLoader`
   - `AutoApprovalGate` (trivial)
   - `StepExecutor` with stub/mock adapters
   - `WorkflowRunner`
   - `ManualApprovalGate` (Spectre.Console UI — do last in Engine)
3. **`KataFlow.Infrastructure`** — `SessionStore` (JSON), `ArtifactStore`, `FileWatcher`.
4. **`KataFlow.Adapters`**
   - `FileDropChannel` (file system only, no API calls)
   - `ClaudeAdapter` + `ClaudeApiChannel` (Anthropic SDK)
   - `RestAdapter` + `RestApiChannel` (HttpClient, OpenAI-compatible)
5. **`KataFlow.Cli`** — wire commands with `System.CommandLine`; add Spectre.Console table/progress output.

**Unit tests** (write before moving to next layer): `PromptRenderer`, `ContextBuilder`, `YamlWorkflowLoader`, `StepExecutor` (mocked adapters), `WorkflowRunner` (mocked adapters + gates).

---

## 24. v2 Design Notes (out of scope for v1 implementation)

### Parallel workers & git worktree isolation

When `StepType.Parallel` is implemented:

1. The runner reads `step.WorkerCount` and spawns N concurrent `StepExecutor` instances.
2. If `step.UseWorktree = true`, each worker gets an isolated git working tree:
   ```bash
   git worktree add .worktrees/{session-id}-{step}-{n} -b agent/{session}/{step}/{n}
   ```
3. Each worker's FileDrop task/output files live inside its worktree.
4. After all workers complete, a consolidation step runs in the main tree (defined in `DependsOn`).
5. Worktrees are deleted after the consolidation step succeeds.

`StepDefinition` already carries `Type`, `WorkerCount`, `DependsOn`, and `UseWorktree` so they serialize in YAML and session state without a breaking change when v2 lands.

### Other v2 items

- Web UI / dashboard
- Remote/networked session store
- Agent-to-agent direct messaging
- Multi-session parallel execution
- Plugin system beyond the adapter pattern
