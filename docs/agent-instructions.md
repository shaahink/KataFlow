# KataFlow — Agent Implementation Instructions

> **Agent:** DeepSeek V4 Pro  
> **Date:** 2026-06-08  
> **Status:** Implementation-ready — do not deviate from this spec  

---

## 0. How to Read This Document

Each task has:
- **What** — the change in plain terms
- **Why** — so you understand the intent and can make judgment calls
- **Exactly how** — file paths, method signatures, code

Implement tasks in the numbered order given. Do not skip tasks or reorder them — later tasks depend on earlier ones. Write tests as specified per task before moving to the next. Do not refactor code that is not touched by a task.

---

## 1. Codebase Overview

KataFlow is a .NET 10 / C# 14 multi-agent orchestration CLI + API + Angular Web UI. Solution: `KataFlow.sln`.

### Projects

| Project | Purpose | Status |
|---|---|---|
| `KataFlow.Core` | Models, enums, interfaces. Zero deps. | Complete |
| `KataFlow.Engine` | WorkflowRunner, StepExecutor, gates, loaders | Complete |
| `KataFlow.Infrastructure` | SessionStore (JSON), ArtifactStore, FileWatcher | Complete |
| `KataFlow.Adapters` | FileDrop, CliExecute, Claude (API), Rest (API) channels | Partial — see tasks |
| `KataFlow.ServiceDefaults` | Shared DI registration | Partial — see tasks |
| `KataFlow.Cli` | CLI commands (run, approve, status, watch, list) | Partial — see tasks |
| `KataFlow.Api` | ASP.NET Minimal API + SignalR | Partial — see tasks |
| `KataFlow.AppHost` | .NET Aspire orchestration | Broken — see Task 8 |
| `KataFlow.Web` | Angular 19 SPA | Partial — see tasks |
| `KataFlow.Tests` | xUnit unit + integration tests | Partial — see tasks |

### Key invariants — do not violate

- `KataFlow.Core` has zero dependencies on any other KataFlow project. Never add them.
- All interfaces in `KataFlow.Core/Interfaces/`. Implementations in other projects.
- `KataFlow.ServiceDefaults` is the single DI registration point shared by CLI and API.
- Templates are Markdown files in `./templates/`. Workflows are YAML in `./workflows/`.
- Sessions are stored as JSON in `./sessions/{session-id}/session.json`.
- Artifacts are stored in `./sessions/{session-id}/artifacts/{name}.md`.
- API keys come from env vars (`ANTHROPIC_API_KEY`, `DEEPSEEK_API_KEY`) or `.env` file — never from config in source.
- `Session` is a `class` (not `record`) — mutable aggregate.
- Model ID is `claude-sonnet-4-6` (not `claude-opus-4-6` — that ID doesn't exist).

---

## 2. Task List

---

### Task 1 — Fix `CliExecuteChannel`: real Claude Code & OpenCode support

**What:** Redesign `CliExecuteChannel` and `CliExecuteOptions` to support stdin-based invocation (for `claude --print`) in addition to the current file-based mode. Add the channel to both adapters' supported channel list. Update `appsettings.json` with correct defaults.

**Why:** The current implementation passes the input file path via an argument template (`--prompt "{input}"`). Claude Code (`claude --print`) and OpenCode both support receiving a prompt via stdin, which is cleaner and more universal. The channel is currently only wired to the RestAdapter — it should also be available for ClaudeAdapter (so users can drive Claude Code CLI as the Claude executor).

**Files to modify:**

**`src/KataFlow.Adapters/CliExecute/CliExecuteOptions.cs`** — replace entirely:

```csharp
namespace KataFlow.Adapters.CliExecute;

public class CliExecuteOptions
{
    /// <summary>CLI command to execute (e.g. "claude", "opencode", "bash").</summary>
    public string Command { get; set; } = "claude";

    /// <summary>Arguments passed to the command (before any input injection).</summary>
    public string Arguments { get; set; } = "--print";

    /// <summary>
    /// How the rendered prompt is delivered to the CLI process.
    /// Stdin: pipe prompt to process stdin (use for "claude --print", "opencode").
    /// File: write prompt to a temp file and append its path to arguments (legacy behaviour).
    /// </summary>
    public CliInputMode InputMode { get; set; } = CliInputMode.Stdin;

    public int TimeoutSeconds { get; set; } = 600;
}

public enum CliInputMode { Stdin, File }
```

**`src/KataFlow.Adapters/CliExecute/CliExecuteChannel.cs`** — replace `SendAsync` body:

```csharp
public async Task<AgentResponse> SendAsync(AgentRequest request, CancellationToken ct = default)
{
    var sessionDir = _fileSystem.Combine(
        _fileSystem.GetCurrentDirectory(), "sessions", request.SessionId);
    _fileSystem.CreateDirectory(sessionDir);

    var finalPrompt = AppendOutputInstructions(request, sessionDir);

    _logger.LogInformation("CliExecute: {Command} {Args} (mode={Mode})",
        _options.Command, _options.Arguments, _options.InputMode);

    try
    {
        BufferedCommandResult result;

        if (_options.InputMode == CliInputMode.Stdin)
        {
            result = await Cli.Wrap(_options.Command)
                .WithArguments(_options.Arguments)
                .WithStandardInputPipe(PipeSource.FromString(finalPrompt))
                .WithWorkingDirectory(_fileSystem.GetCurrentDirectory())
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(ct);
        }
        else // File
        {
            var inputFile = _fileSystem.Combine(sessionDir, $"input-{request.StepName}.md");
            await _fileSystem.WriteAllTextAsync(inputFile, finalPrompt, ct);
            _logger.LogInformation("Input file written: {Path}", inputFile);

            var args = _options.Arguments.Contains("{input}", StringComparison.OrdinalIgnoreCase)
                ? _options.Arguments.Replace("{input}", inputFile, StringComparison.OrdinalIgnoreCase)
                : $"{_options.Arguments} \"{inputFile}\"";

            result = await Cli.Wrap(_options.Command)
                .WithArguments(args)
                .WithWorkingDirectory(_fileSystem.GetCurrentDirectory())
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(ct);
        }

        var content = string.IsNullOrWhiteSpace(result.StandardOutput)
            ? result.StandardError
            : result.StandardOutput;

        return new AgentResponse
        {
            Content = content,
            Success = result.ExitCode == 0 && !string.IsNullOrWhiteSpace(content),
            ErrorMessage = result.ExitCode != 0
                ? $"Exit {result.ExitCode}: {result.StandardError}"
                : null,
            Metadata = new() { ["exit_code"] = result.ExitCode.ToString(), ["command"] = _options.Command },
        };
    }
    catch (OperationCanceledException)
    {
        return new AgentResponse { Content = "", Success = false, ErrorMessage = "CLI execution timed out" };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "CLI execution failed for step {Step}", request.StepName);
        return new AgentResponse { Content = "", Success = false, ErrorMessage = ex.Message };
    }
}

private string AppendOutputInstructions(AgentRequest request, string sessionDir)
{
    var instructionsPath = _fileSystem.Combine(_templatesPath, "_system", "output-instructions.md");
    if (!_fileSystem.FileExists(instructionsPath))
        return request.RenderedPrompt;

    var outputFile = _fileSystem.Combine(sessionDir, $"output-{request.StepName}.md");
    var vars = new Dictionary<string, string>(request.Metadata)
    {
        ["_output_path"] = outputFile,
        ["_session_id"] = request.SessionId,
        ["_step_name"] = request.StepName,
    };

    var raw = _fileSystem.ReadAllTextAsync(instructionsPath).GetAwaiter().GetResult();
    foreach (var (k, v) in vars)
        raw = raw.Replace($"{{{{{k}}}}}", v, StringComparison.OrdinalIgnoreCase);

    return request.RenderedPrompt + "\n\n" + raw;
}
```

Note: remove the field `_templatesPath` and `_options` are already constructor params — keep constructor signature unchanged but remove the `_options.ArgumentsTemplate` usage.

**`src/KataFlow.ServiceDefaults/KataFlowServiceExtensions.cs`** — update `AddKataFlowClaude` and `AddKataFlowRest` to include `CliExecuteChannel` in both adapters:

```csharp
// In AddKataFlowClaude:
services.AddSingleton<IAgentAdapter>(sp =>
    new ClaudeAdapter([
        sp.GetRequiredService<FileDropChannel>(),
        sp.GetRequiredService<CliExecuteChannel>(),   // ADD
        sp.GetRequiredService<ClaudeApiChannel>(),
    ]));

// In AddKataFlowRest:
services.AddSingleton<IAgentAdapter>(sp =>
    new RestAdapter([
        sp.GetRequiredService<FileDropChannel>(),     // ADD
        sp.GetRequiredService<CliExecuteChannel>(),   // ADD
        sp.GetRequiredService<RestApiChannel>(),
    ]));
```

**`src/KataFlow.Cli/appsettings.json`** and **`src/KataFlow.Api/appsettings.json`** — update the `CliExecute` section:

```json
"CliExecute": {
  "Command": "claude",
  "Arguments": "--print",
  "InputMode": "Stdin",
  "TimeoutSeconds": 600
}
```

Remove the old `"ArgumentsTemplate"` key.

**Tests:** Add unit tests in `tests/KataFlow.Tests/CliExecuteChannelTests.cs`:
- `SendAsync_Stdin_WritesPromptToStdin` — mock `Cli.Wrap`; verify stdin receives the rendered prompt
- `SendAsync_File_WritesFileAndPassesPath` — verify file is written and path is in args
- `SendAsync_AppendsOutputInstructions_WhenTemplateExists`

---

### Task 2 — Add `Script` agent type for shell commands

**What:** Add `AgentType.Script` and `StepDefinition.ScriptCommand`. When a step has `Agent = Script`, `StepExecutor` runs the command directly via `CliWrap` without going through an adapter, captures stdout as the step output, and stores it as an artifact.

**Why:** Agentic workflows need to run `dotnet test`, `dotnet build`, `npm run lint`, etc. as pipeline steps and capture the output as an artifact that the review step can read. This should not require writing a prompt template or spinning up an LLM.

**Files to modify:**

**`src/KataFlow.Core/Enums/AgentType.cs`**:
```csharp
public enum AgentType { Claude, Rest, Script }
```

**`src/KataFlow.Core/Models/StepDefinition.cs`** — add one property (keep all existing):
```csharp
/// <summary>Shell command to run when Agent = Script. Supports {{variable}} substitution.</summary>
public string? ScriptCommand { get; init; }
```

**`src/KataFlow.Engine/StepExecutor.cs`** — at the top of `ExecuteAsync`, before the `adapterResolver` call, add:

```csharp
if (step.Agent == AgentType.Script)
    return await ExecuteScriptStepAsync(session, step, ct);
```

Add a new private method to `StepExecutor`:

```csharp
private async Task<StepResult> ExecuteScriptStepAsync(
    Session session, StepDefinition step, CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(step.ScriptCommand))
        throw new InvalidOperationException($"Step '{step.Name}' has Agent=Script but no ScriptCommand.");

    var vars = _contextBuilder.Build(session, step);
    var command = _promptRenderer.Render(step.ScriptCommand, vars);

    _logger.LogInformation("Script step {Step}: {Command}", step.Name, command);

    // Split into executable + args on the first space
    var spaceIdx = command.IndexOf(' ');
    var exe = spaceIdx < 0 ? command : command[..spaceIdx];
    var argStr = spaceIdx < 0 ? "" : command[(spaceIdx + 1)..];

    try
    {
        var result = await Cli.Wrap(exe)
            .WithArguments(argStr)
            .WithWorkingDirectory(Environment.CurrentDirectory)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(ct);

        var output = string.IsNullOrWhiteSpace(result.StandardOutput)
            ? result.StandardError
            : result.StandardOutput;

        if (!string.IsNullOrWhiteSpace(result.StandardError) && result.ExitCode != 0)
            output = result.StandardOutput + "\n--- STDERR ---\n" + result.StandardError;

        var success = result.ExitCode == 0;

        if (step.OutputArtifactName is not null)
        {
            await _artifactStore.SaveAsync(session, step.OutputArtifactName, output);
            session.Artifacts[step.OutputArtifactName] = _artifactStore.GetPath(session, step.OutputArtifactName);
        }

        session.History.Add(new SessionStep
        {
            StepName = step.Name,
            Status = success ? SessionStatus.Complete : SessionStatus.Failed,
            OutputArtifactPath = step.OutputArtifactName is not null
                ? session.Artifacts.GetValueOrDefault(step.OutputArtifactName)
                : null,
            CompletedAt = DateTimeOffset.UtcNow,
        });

        return new StepResult
        {
            StepName = step.Name,
            Success = success,
            ArtifactPath = step.OutputArtifactName is not null
                ? session.Artifacts.GetValueOrDefault(step.OutputArtifactName)
                : null,
            ArtifactContent = output,
            ErrorMessage = success ? null : $"Script exited with code {result.ExitCode}",
        };
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        _logger.LogError(ex, "Script step {Step} failed", step.Name);
        session.History.Add(new SessionStep
        {
            StepName = step.Name, Status = SessionStatus.Failed,
            ErrorMessage = ex.Message, CompletedAt = DateTimeOffset.UtcNow,
        });
        return new StepResult { StepName = step.Name, Success = false, ErrorMessage = ex.Message };
    }
}
```

Note: `CliWrap` is already a dependency in `KataFlow.Adapters` — you need to add the `CliWrap` NuGet package reference to `KataFlow.Engine` as well:
```
dotnet add src/KataFlow.Engine/KataFlow.Engine.csproj package CliWrap
```

**`src/KataFlow.Engine/Loaders/YamlWorkflowLoader.cs`** — find where `StepDefinition` is deserialized from YAML and add handling for `script_command`:

In the YAML step mapping, after parsing all existing fields, add:
```csharp
ScriptCommand = stepNode["script_command"]?.AsString(),
```

Also ensure `agent` field defaults to `Rest` when not present, and when `agent: script` is in the YAML, map it to `AgentType.Script`:
```csharp
Agent = stepNode["agent"]?.AsString()?.ToLowerInvariant() switch
{
    "claude" => AgentType.Claude,
    "script" => AgentType.Script,
    _ => AgentType.Rest,
},
```

Also make `prompt_template` optional (empty string) when agent is Script (it's unused for Script steps):
```csharp
PromptTemplate = stepNode["prompt_template"]?.AsString() ?? "",
```

**`src/KataFlow.Engine/WorkflowBuilder.cs`** — add a fluent method for script steps:

```csharp
// On the step builder class, add:
public StepBuilder AsScript(string command)
{
    _step.Agent = AgentType.Script;
    _step.ScriptCommand = command;
    _step.PromptTemplate = "";
    return this;
}
```

**`src/KataFlow.ServiceDefaults/KataFlowServiceExtensions.cs`** — in `AddKataFlowRunner`, add `AgentType.Script` to the resolver (it will never be called but should not throw):

```csharp
services.AddSingleton<Func<AgentType, IAgentAdapter>>(sp => agentType => agentType switch
{
    AgentType.Claude => sp.GetServices<IAgentAdapter>().First(a => a is ClaudeAdapter),
    AgentType.Rest   => sp.GetServices<IAgentAdapter>().First(a => a is RestAdapter),
    AgentType.Script => throw new InvalidOperationException("Script steps do not use an adapter"),
    _ => throw new InvalidOperationException($"Unknown agent type: {agentType}"),
});
```

**Tests:** `tests/KataFlow.Tests/ScriptStepTests.cs`
- `ExecuteScriptStep_Success_CapturesStdout`
- `ExecuteScriptStep_NonZeroExit_ReturnsFailure`
- `ExecuteScriptStep_RendersCommandVariables`
- `ExecuteScriptStep_MissingScriptCommand_Throws`

---

### Task 3 — Budget & token tracking

**What:** Track input/output tokens and estimated USD cost per step. Accumulate them on the session. Display in CLI approval gate, `status` command, and expose via the API. Support an optional budget cap that warns (but does not hard-fail) when exceeded.

**Why:** The user needs to know what each workflow run costs, especially when running multiple steps against Claude API.

**Model pricing rates (as of June 2026 — hardcode these; the operator can override in config):**

| Model | Input $/1M | Output $/1M |
|---|---|---|
| `claude-sonnet-4-6` | 3.00 | 15.00 |
| `claude-haiku-4-5-20251001` | 0.80 | 4.00 |
| `claude-opus-4-8` | 15.00 | 75.00 |
| `deepseek-chat` | 0.14 | 0.28 |
| `deepseek-reasoner` | 0.55 | 2.19 |
| `gpt-4o` | 2.50 | 10.00 |

**New file: `src/KataFlow.Core/Models/StepBudget.cs`**:

```csharp
namespace KataFlow.Core.Models;

public class StepBudget
{
    public required string StepName { get; init; }
    public string Model { get; init; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal CostUsd { get; set; }
}
```

**`src/KataFlow.Core/Constants.cs`** — add pricing table (new static class at the bottom of the file):

```csharp
public static class ModelPricing
{
    private static readonly Dictionary<string, (decimal InputPer1M, decimal OutputPer1M)> Rates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["claude-sonnet-4-6"]           = (3.00m,  15.00m),
            ["claude-haiku-4-5-20251001"]   = (0.80m,   4.00m),
            ["claude-opus-4-8"]             = (15.00m,  75.00m),
            ["deepseek-chat"]               = (0.14m,   0.28m),
            ["deepseek-reasoner"]           = (0.55m,   2.19m),
            ["gpt-4o"]                      = (2.50m,  10.00m),
        };

    public static decimal Calculate(string model, int inputTokens, int outputTokens)
    {
        if (!Rates.TryGetValue(model, out var rate)) return 0m;
        return (inputTokens * rate.InputPer1M + outputTokens * rate.OutputPer1M) / 1_000_000m;
    }

    public static bool IsKnown(string model) => Rates.ContainsKey(model);
}
```

**`src/KataFlow.Core/Models/Session.cs`** — add two properties (after `History`):

```csharp
public List<StepBudget> Budget { get; } = new();
public decimal? BudgetCapUsd { get; set; }

// Computed — not serialized, derived at read time
[System.Text.Json.Serialization.JsonIgnore]
public decimal TotalCostUsd => Budget.Sum(b => b.CostUsd);

[System.Text.Json.Serialization.JsonIgnore]
public int TotalInputTokens => Budget.Sum(b => b.InputTokens);

[System.Text.Json.Serialization.JsonIgnore]
public int TotalOutputTokens => Budget.Sum(b => b.OutputTokens);
```

**`src/KataFlow.Core/Models/StepResult.cs`** — add one property:

```csharp
public StepBudget? Budget { get; init; }
```

**`src/KataFlow.Adapters/Claude/ClaudeApiChannel.cs`** — update the `return new AgentResponse` at the end of `SendAsync` — the metadata keys are already there (`usage_input_tokens`, `usage_output_tokens`). Verify they are populated from `result.Usage.InputTokens` and `result.Usage.OutputTokens`. No change needed if already correct.

**`src/KataFlow.Adapters/Rest/RestApiChannel.cs`** — add token extraction (it's currently missing). After deserializing `result`, add:

```csharp
// Add to the inner ChatCompletionResponse class:
[JsonPropertyName("usage")]
public CompletionUsage? Usage { get; set; }

private class CompletionUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
}
```

Update the return statement to include metadata:
```csharp
Metadata = new()
{
    ["model"] = result?.Model ?? stepModel,
    ["usage_input_tokens"] = result?.Usage?.PromptTokens.ToString() ?? "0",
    ["usage_output_tokens"] = result?.Usage?.CompletionTokens.ToString() ?? "0",
},
```

**`src/KataFlow.Engine/StepExecutor.cs`** — after receiving `AgentResponse` (line after `if (!response.Success...)` check), create a budget entry and attach it to the step result:

```csharp
// After adapter.SendAsync returns:
var model = response.Metadata.TryGetValue("model", out var m) ? m : step.Model ?? "";
var inTok  = int.TryParse(response.Metadata.TryGetValue("usage_input_tokens",  out var it) ? it : "", out var i) ? i : 0;
var outTok = int.TryParse(response.Metadata.TryGetValue("usage_output_tokens", out var ot) ? ot : "", out var o) ? o : 0;

var budget = new StepBudget
{
    StepName    = step.Name,
    Model       = model,
    InputTokens = inTok,
    OutputTokens = outTok,
    CostUsd     = ModelPricing.Calculate(model, inTok, outTok),
};
session.Budget.Add(budget);

// Log a warning if budget cap exceeded:
if (session.BudgetCapUsd.HasValue && session.TotalCostUsd > session.BudgetCapUsd)
    _logger.LogWarning("Budget cap exceeded: ${Total:F4} > ${Cap:F4}",
        session.TotalCostUsd, session.BudgetCapUsd);
```

In the `return new StepResult { ... }` block, add:
```csharp
Budget = budget,
```

**`src/KataFlow.Engine/Gates/ManualApprovalGate.cs`** — in `ShowApprovalPrompt`, add budget info to the panel:

```csharp
// Add parameter: StepBudget? budget = null
// In the panel markup, append:
+ (budget is not null && budget.CostUsd > 0
    ? $"\n\n[dim]Step cost: [/][yellow]${budget.CostUsd:F4}[/] [dim]({budget.InputTokens} in / {budget.OutputTokens} out)[/]"
    : "")
```

Update the `RequestApprovalAsync` signature to take the budget from `StepResult.Budget`:
```csharp
var decision = await ShowApprovalPrompt(result, result.Budget);
```

**`src/KataFlow.Cli/Commands/StatusCommand.cs`** — in the session detail display, add a budget summary table row showing total cost, total tokens, and per-step cost breakdown.

**`src/KataFlow.Api/Endpoints/SessionEndpoints.cs`** — in `GET /api/sessions/{id}`, add to the response object:

```csharp
Budget = new
{
    TotalCostUsd = session.TotalCostUsd,
    TotalInputTokens = session.TotalInputTokens,
    TotalOutputTokens = session.TotalOutputTokens,
    Steps = session.Budget.Select(b => new
    {
        b.StepName, b.Model, b.InputTokens, b.OutputTokens, b.CostUsd
    }),
},
```

**`src/KataFlow.Cli/Commands/RunCommand.cs`** — support `--budget-cap <amount>` option:

```csharp
var budgetCapOption = new Option<decimal?>("--budget-cap")
{
    Description = "Warn when session cost exceeds this USD amount"
};
// After loading session or creating context:
if (budgetCap.HasValue)
    session.BudgetCapUsd = budgetCap;
```

Wait — the session is created inside `WorkflowRunner.RunAsync` via `SessionManager.ResolveAsync`. To pass the budget cap, add `BudgetCapUsd` to `SessionContext`:

**`src/KataFlow.Core/Models/SessionContext.cs`** — add:
```csharp
public decimal? BudgetCapUsd { get; init; }
```

**`src/KataFlow.Engine/SessionManager.cs`** — in `ResolveAsync`, after merging variables:
```csharp
if (ctx.BudgetCapUsd.HasValue)
    session.BudgetCapUsd = ctx.BudgetCapUsd;
```

**Tests:** `tests/KataFlow.Tests/BudgetTests.cs`
- `ModelPricing_KnownModel_CalculatesCorrectly` — verify claude-sonnet-4-6 at 1000 in + 500 out = (3.00 + 7.50)/1M = $0.0000105
- `StepExecutor_PopulatesBudget_OnApiResponse`
- `Session_TotalCostUsd_SumsStepBudgets`

---

### Task 4 — Fix `WaitingApproval` state & session resume

**What:** When a `ManualApprovalGate` is about to block for interactive input, set `session.Status = WaitingApproval` and persist. When `kataflow run --session` resumes a `WaitingApproval` session, skip re-executing the step and go directly to the approval gate using the saved artifact.

**Why:** Currently, if the terminal is closed during a manual approval prompt, the session is stuck in `Running` status and the step will be re-executed on resume (wasting tokens). The fix ensures resume always goes to the gate, not the executor.

**`src/KataFlow.Engine/WorkflowRunner.cs`** — update the inner loop:

```csharp
foreach (var step in workflow.Steps.Skip(session.CurrentStepIndex))
{
    if (ct.IsCancellationRequested)
        return await _sessionManager.CancelAsync(session);

    if (step.Type == StepType.Parallel)
    {
        _logger.LogWarning("Parallel steps not supported in v1, skipping {Step}", step.Name);
        continue;
    }

    using var stepScope = _logger.BeginScope(new Dictionary<string, object>
        { [Diagnostics.Tags.StepName] = step.Name });

    StepResult stepResult;

    // Resume: step was already executed, waiting for approval decision
    if (session.Status == SessionStatus.WaitingApproval
        && session.History.Any(h => h.StepName == step.Name && h.Status == SessionStatus.Complete))
    {
        _logger.LogInformation("Resuming approval gate for step {StepName}", step.Name);
        var hist = session.History.Last(h => h.StepName == step.Name);
        var content = hist.OutputArtifactPath is not null && File.Exists(hist.OutputArtifactPath)
            ? await File.ReadAllTextAsync(hist.OutputArtifactPath, ct)
            : "";
        stepResult = new StepResult
        {
            StepName = step.Name, Success = true,
            ArtifactPath = hist.OutputArtifactPath,
            ArtifactContent = content,
        };
        session.Status = SessionStatus.Running;
        await _sessionManager.PersistAsync(session);
    }
    else
    {
        _logger.LogInformation("Executing step {StepName} ({Role})", step.Name, step.Role);
        stepResult = await _stepExecutor.ExecuteAsync(session, step, _adapterResolver, ct);
    }

    if (!stepResult.Success)
    {
        _logger.LogError("Step {StepName} failed: {Error}", step.Name, stepResult.ErrorMessage);
        return await _sessionManager.FailAsync(session, stepResult.ErrorMessage!);
    }

    _logger.LogInformation("Step {StepName} completed", step.Name);

    var gateMode = ctx.AutoApprove ? ApprovalMode.Auto : step.Approval;
    if (!_gates.TryGetValue(gateMode, out var gate))
        return await _sessionManager.FailAsync(session, $"No gate for {gateMode}");

    // Set WaitingApproval before blocking on manual gate
    if (gateMode == ApprovalMode.Manual)
    {
        session.Status = SessionStatus.WaitingApproval;
        await _sessionManager.PersistAsync(session);
    }

    var decision = await gate.RequestApprovalAsync(stepResult, ct);

    if (decision == ApprovalDecision.Reject)
    {
        _logger.LogInformation("Step {StepName} rejected by operator", step.Name);
        return await _sessionManager.FailAsync(session, "Rejected by operator");
    }

    session.Status = SessionStatus.Running;
    session.CurrentStepIndex++;
    await _sessionManager.PersistAsync(session);
}
```

Note: `ctx` (the `SessionContext`) must be accessible in `RunAsync` — it already is from the method parameter.

**Tests:** Add to `tests/KataFlow.Tests/WorkflowRunnerTests.cs`:
- `RunAsync_WaitingApproval_SkipsStepReExecution` — verify step executor is NOT called when session.Status == WaitingApproval and step has a Complete history entry
- `RunAsync_SetsWaitingApproval_BeforeManualGate`

---

### Task 5 — Fix `WatchCommand`

**What:** Replace the broken log-file tailing implementation with a live session.json watcher that polls and prints step/status updates as they happen.

**Why:** The current implementation looks for `orchestration-*.log` files that are never created. The session JSON at `sessions/{id}/session.json` is updated after every step — polling it is the correct way to observe a live session.

**`src/KataFlow.Cli/Commands/WatchCommand.cs`** — replace `SetAction` body:

```csharp
command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
{
    var sessionId = parseResult.GetRequiredValue(sessionOption);
    var sessionFile = _fileSystem.Combine(
        _fileSystem.GetCurrentDirectory(), "sessions", sessionId, "session.json");

    if (!_fileSystem.FileExists(sessionFile))
    {
        Console.Error.WriteLine($"Session not found: {sessionId}");
        return 1;
    }

    Console.WriteLine($"Watching session: {sessionId}");
    Console.WriteLine("Press Ctrl+C to stop.\n");

    var lastStatus = "";
    var lastStepIndex = -1;
    var lastBudget = "";

    while (!ct.IsCancellationRequested)
    {
        try
        {
            var json = await _fileSystem.ReadAllTextAsync(sessionFile, ct);
            var session = System.Text.Json.JsonSerializer.Deserialize<Session>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (session is null) { await Task.Delay(1000, ct); continue; }

            var statusLine = $"[{DateTimeOffset.Now:HH:mm:ss}] Status: {session.Status}  Step: {session.CurrentStepIndex}";
            var budgetLine = session.TotalCostUsd > 0
                ? $"  Cost: ${session.TotalCostUsd:F4}  Tokens: {session.TotalInputTokens}in/{session.TotalOutputTokens}out"
                : "";

            if (session.Status.ToString() != lastStatus
                || session.CurrentStepIndex != lastStepIndex
                || budgetLine != lastBudget)
            {
                Console.WriteLine(statusLine + budgetLine);

                // Print latest history entry
                var latest = session.History.LastOrDefault();
                if (latest is not null)
                    Console.WriteLine($"  Last step: {latest.StepName} → {latest.Status}");

                lastStatus = session.Status.ToString();
                lastStepIndex = session.CurrentStepIndex;
                lastBudget = budgetLine;
            }

            if (session.Status is SessionStatus.Complete or SessionStatus.Failed or SessionStatus.Cancelled)
            {
                Console.WriteLine($"\nSession finished: {session.Status}");
                if (session.TotalCostUsd > 0)
                    Console.WriteLine($"Total cost: ${session.TotalCostUsd:F4}");
                break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // session file may be mid-write; ignore and retry
        }

        await Task.Delay(1500, ct);
    }

    return 0;
});
```

Add `using KataFlow.Core.Models;` and `using KataFlow.Core.Enums;` to the file.

---

### Task 6 — Add `[E] Edit` to `ManualApprovalGate`

**What:** Add an Edit option to the approval prompt that opens the artifact in `$EDITOR` (or `notepad` on Windows), waits for the file to be saved/closed, re-reads it, updates the artifact on disk, and then approves.

**Why:** The spec defines this as a key feature. The operator may want to clean up or correct an agent output before continuing.

**`src/KataFlow.Engine/Gates/ManualApprovalGate.cs`** — update `ShowApprovalPrompt`:

```csharp
private static Task<ApprovalDecision> ShowApprovalPrompt(StepResult result, StepBudget? budget = null)
{
    // ... panel rendering unchanged ...

    while (true)
    {
        var choices = new List<string> { "Approve and continue", "Reject and stop", "View full artifact" };
        if (result.ArtifactPath is not null)
            choices.Add("Edit artifact then approve");

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choose action:")
                .PageSize(6)
                .AddChoices(choices));

        if (choice == "Reject and stop")
            return Task.FromResult(ApprovalDecision.Reject);

        if (choice == "View full artifact" && result.ArtifactContent is not null)
        {
            AnsiConsole.Write(new Panel(result.ArtifactContent)
            {
                Header = new PanelHeader(result.StepName),
                Border = BoxBorder.Heavy,
            });
            continue; // loop back to prompt
        }

        if (choice == "Edit artifact then approve" && result.ArtifactPath is not null)
        {
            OpenInEditor(result.ArtifactPath);
            AnsiConsole.MarkupLine("[green]Artifact saved. Approving.[/]");
            return Task.FromResult(ApprovalDecision.Approve);
        }

        return Task.FromResult(ApprovalDecision.Approve);
    }
}

private static void OpenInEditor(string filePath)
{
    var editor = Environment.GetEnvironmentVariable("EDITOR")
        ?? (OperatingSystem.IsWindows() ? "notepad" : "nano");
    try
    {
        var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = editor,
            Arguments = $"\"{filePath}\"",
            UseShellExecute = true,
        });
        p?.WaitForExit();
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Could not open editor '{editor}': {ex.Message}[/]");
    }
}
```

---

### Task 7 — Fix API: missing gates + artifact content endpoint

**What:**  
1. `AddKataFlowAll` does not register approval gates. The `WorkflowRunner` requires `IEnumerable<IApprovalGate>` — if none are registered, it gets an empty list and the gate lookup fails silently. Fix by always registering `AutoApprovalGate` in `AddKataFlowAll`.  
2. Add `GET /api/sessions/{id}/artifacts/{name}` endpoint that reads and returns artifact content.  
3. Ensure the `RunEndpoints.Map` session creation + run is thread-safe (the background `Task.Run` may race with other requests on the same session — document this; a full fix is out of scope for v1).

**`src/KataFlow.ServiceDefaults/KataFlowServiceExtensions.cs`** — in `AddKataFlowAll`, add:

```csharp
services.AddSingleton<IApprovalGate, AutoApprovalGate>();
// Note: ManualApprovalGate is only added by CLI (requires Spectre.Console terminal)
```

**`src/KataFlow.Api/Endpoints/SessionEndpoints.cs`** — add new endpoint inside `Map`:

```csharp
app.MapGet("/api/sessions/{id}/artifacts/{name}", async (string id, string name, ISessionStore store, IFileSystem fs) =>
{
    var session = await store.GetAsync(id);
    if (session is null) return Results.NotFound();
    if (!session.Artifacts.TryGetValue(name, out var path)) return Results.NotFound();
    if (!fs.FileExists(path)) return Results.NotFound();
    var content = await fs.ReadAllTextAsync(path);
    return Results.Ok(new { name, content, path });
});
```

**`src/KataFlow.Api/Endpoints/SessionEndpoints.cs`** — in `GET /api/sessions/{id}`, add budget to the response (from Task 3):

Merge this change with the session endpoint update in Task 3.

---

### Task 8 — Fix Aspire AppHost

**What:** Fix `KataFlow.AppHost/Program.cs` to: pass API keys from env to the API process; wait for the API to be healthy before the web app starts; add a health check endpoint to the API; remove the broken `WithReference` on the npm app.

**`src/KataFlow.AppHost/Program.cs`** — replace entirely:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.KataFlow_Api>("api")
    .WithEnvironment("ASPNETCORE_URLS", "http://localhost:5100")
    .WithEnvironment("ANTHROPIC_API_KEY", builder.Configuration["ANTHROPIC_API_KEY"] ?? "")
    .WithEnvironment("DEEPSEEK_API_KEY", builder.Configuration["DEEPSEEK_API_KEY"] ?? "")
    .WithEnvironment("OPENAI_API_KEY", builder.Configuration["OPENAI_API_KEY"] ?? "");

builder.AddNpmApp("web", Path.Combine("..", "KataFlow.Web"), "start")
    .WaitFor(api)
    .WithArgs("--port", "4200", "--proxy-config", "proxy.conf.json");

builder.Build().Run();
```

**`src/KataFlow.Api/Program.cs`** — add a health check endpoint (after `app.UseCors()`):

```csharp
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }));
```

**`src/KataFlow.Api/appsettings.json`** — ensure `ASPNETCORE_URLS` is not hardcoded; it is passed by the AppHost via environment.

---

### Task 9 — Standard agentic workflows & templates

**What:** Add `agentic-dev`, `bug-fix`, and `code-review` preset workflows with matching templates in `templates/agentic/`. These should be baked-in presets (C# classes) as well as YAML files in `workflows/`. The templates must be genuinely useful — not stubs with just `{{project}}`.

**New YAML: `workflows/agentic-dev.yaml`**

```yaml
workflow:
  name: agentic-dev
  description: "Standard agentic programming: plan (Claude) → implement (CLI agent) → build → test → review (Claude) → report"
  default_mode: dev

  variables:
    project: ""
    task: ""
    codebase: ""
    constraints: ""
    build_command: "dotnet build --no-restore 2>&1"
    test_command: "dotnet test --no-build 2>&1"

  steps:
    - name: plan
      agent: claude
      role: planner
      prompt_template: templates/agentic/planner.md
      approval: manual
      output_artifact: plan
      timeout_minutes: 15

    - name: implement
      agent: rest
      role: executor
      channel_override: cli_execute
      prompt_template: templates/agentic/executor.md
      context_artifacts: [plan]
      approval: manual
      output_artifact: implementation
      timeout_minutes: 60

    - name: build
      agent: script
      role: verifier
      script_command: "{{build_command}}"
      approval: auto
      output_artifact: build-output
      timeout_minutes: 5

    - name: test
      agent: script
      role: tester
      script_command: "{{test_command}}"
      approval: auto
      output_artifact: test-results
      timeout_minutes: 10

    - name: review
      agent: claude
      role: reviewer
      prompt_template: templates/agentic/reviewer.md
      context_artifacts: [plan, implementation, build-output, test-results]
      approval: manual
      output_artifact: review
      timeout_minutes: 10

    - name: report
      agent: claude
      role: reporter
      prompt_template: templates/agentic/reporter.md
      context_artifacts: [plan, implementation, test-results, review]
      approval: auto
      output_artifact: report
      timeout_minutes: 10
```

**New YAML: `workflows/bug-fix.yaml`**

```yaml
workflow:
  name: bug-fix
  description: "Diagnose bug (Claude) → fix (CLI agent) → verify (tests) → report"
  default_mode: dev

  variables:
    project: ""
    bug_description: ""
    error_output: ""
    codebase: ""
    test_command: "dotnet test --no-build 2>&1"

  steps:
    - name: diagnose
      agent: claude
      role: planner
      prompt_template: templates/agentic/diagnoser.md
      approval: manual
      output_artifact: diagnosis
      timeout_minutes: 10

    - name: fix
      agent: rest
      role: executor
      channel_override: cli_execute
      prompt_template: templates/agentic/fixer.md
      context_artifacts: [diagnosis]
      approval: manual
      output_artifact: fix
      timeout_minutes: 45

    - name: verify
      agent: script
      role: tester
      script_command: "{{test_command}}"
      approval: auto
      output_artifact: test-results
      timeout_minutes: 10

    - name: report
      agent: claude
      role: reporter
      prompt_template: templates/agentic/reporter.md
      context_artifacts: [diagnosis, fix, test-results]
      approval: auto
      output_artifact: report
      timeout_minutes: 10
```

**New YAML: `workflows/code-review.yaml`**

```yaml
workflow:
  name: code-review
  description: "Review an implementation artifact and produce a structured report"
  default_mode: headless

  variables:
    project: ""
    implementation: ""

  steps:
    - name: review
      agent: claude
      role: reviewer
      prompt_template: templates/agentic/reviewer.md
      approval: manual
      output_artifact: review
      timeout_minutes: 10

    - name: report
      agent: claude
      role: reporter
      prompt_template: templates/agentic/reporter.md
      context_artifacts: [review]
      approval: auto
      output_artifact: report
      timeout_minutes: 10
```

**New templates in `templates/agentic/`:**

**`templates/agentic/planner.md`:**

```markdown
# Task: Implementation Plan

## Project
{{project}}

## Task
{{task}}

## Codebase Context
{{codebase}}

## Constraints
{{constraints}}

## Instructions

You are a senior software architect. Produce a detailed, actionable implementation plan for the task above.

Your plan must include:

1. **Scope** — exactly what will change and what will not
2. **Component breakdown** — which files/classes/functions need to be created or modified, and why
3. **Implementation steps** — ordered list of changes, each small enough to be implemented and tested independently
4. **Edge cases** — list of non-obvious scenarios to handle
5. **Test approach** — what unit and integration tests should verify the implementation
6. **Open questions** — anything that needs clarification before or during implementation

Format: a single Markdown document starting with `# Plan`. Use `##` sections for each part above. Be specific about file paths and function signatures where you know them. This plan will be handed directly to a code-generation agent, so it must be complete and unambiguous.
```

**`templates/agentic/executor.md`:**

```markdown
# Task: Implement

## Implementation Plan
{{plan}}

## Codebase Context
{{codebase}}

## Instructions

You are a senior software engineer. Implement the plan above exactly as specified.

Rules:
- Follow the component breakdown and implementation steps in order
- Write idiomatic, production-quality code — no TODO comments, no half-finished stubs
- One `## File: path/to/file.ext` section per file you create or modify
- For modified files, include the complete file content (not a diff)
- If you discover an issue with the plan, note it in a `## Notes` section at the end but proceed with the best interpretation
- Do not add features not in the plan

Output format: a single Markdown document. Each file is a `## File: <path>` heading followed by a fenced code block with the file content.
```

**`templates/agentic/reviewer.md`:**

```markdown
# Task: Code Review

## Implementation Plan
{{plan}}

## Implementation
{{implementation}}

## Build Output
{{build-output}}

## Test Results
{{test-results}}

## Instructions

You are a senior software engineer performing a code review. Review the implementation against the plan above.

Evaluate:
1. **Correctness** — does the implementation match the plan? are there logic errors?
2. **Completeness** — are all components from the plan implemented?
3. **Code quality** — naming, structure, idiomatic patterns, no unnecessary complexity
4. **Test coverage** — are the tests from the plan present and meaningful?
5. **Build & test status** — interpret the build output and test results; flag failures
6. **Security** — any obvious vulnerabilities (injection, auth bypass, etc.)

Format:
- Start with `# Review`
- Use `## ✅ Passed` for things done well
- Use `## ⚠️ Issues` for things that need fixing, each as a numbered item with: file path, line/function, description, suggested fix
- Use `## 📋 Summary` with a one-paragraph overall assessment and a `Verdict: APPROVE | REQUEST_CHANGES` on its own line
```

**`templates/agentic/reporter.md`:**

```markdown
# Task: Write Report

## Plan
{{plan}}

## Implementation
{{implementation}}

## Test Results
{{test-results}}

## Review
{{review}}

## Instructions

You are a technical writer. Produce a concise delivery report for the completed work.

Include:
1. **What was built** — one paragraph summary of the feature/fix implemented
2. **How to test it** — numbered steps to exercise the new functionality
3. **Files changed** — bulleted list of modified/created files with one-line descriptions
4. **Known issues** — any items flagged in the review that were not addressed
5. **Next steps** — suggested follow-on work

Format: a single Markdown document starting with `# Report: <brief title>`. Keep it under 500 words.
```

**`templates/agentic/diagnoser.md`:**

```markdown
# Task: Diagnose Bug

## Project
{{project}}

## Bug Description
{{bug_description}}

## Error Output
{{error_output}}

## Codebase Context
{{codebase}}

## Instructions

You are a senior software engineer debugging a reported issue. Diagnose the root cause and produce a fix plan.

Your diagnosis must include:
1. **Root cause** — the specific code path, condition, or assumption that causes the bug
2. **Why it manifests** — how the reported symptoms follow from the root cause
3. **Fix plan** — ordered list of exact changes to make (file, function, what to change and why)
4. **Verification** — which existing tests should now pass, or what new test to write

Format: a single Markdown document starting with `# Diagnosis`. Be specific about file paths and function names.
```

**`templates/agentic/fixer.md`:**

```markdown
# Task: Apply Fix

## Diagnosis
{{diagnosis}}

## Codebase Context
{{codebase}}

## Instructions

You are a senior software engineer. Implement the fix described in the diagnosis above.

Rules:
- Apply only the changes described in the fix plan — do not refactor unrelated code
- One `## File: path/to/file.ext` section per file modified
- Include the complete file content for each modified file
- Write a `## Notes` section if you deviated from the plan and why

Output format: a single Markdown document starting with `# Fix`.
```

**Baked-in presets — `src/KataFlow.Engine/Presets/`:**

Add three new preset classes: `AgenticDevPreset.cs`, `BugFixPreset.cs`, `CodeReviewAgenticPreset.cs` following exactly the same pattern as `SoftwareLifecyclePreset.cs`. Register them in `PresetWorkflowRegistry`:

```csharp
["agentic-dev"]   = AgenticDevPreset.Build(),
["bug-fix"]       = BugFixPreset.Build(),
["code-review-agentic"] = CodeReviewAgenticPreset.Build(),
```

The fluent preset definitions must mirror the YAML files above exactly (same steps, same timeouts, same templates paths).

Note: `WorkflowBuilder` / `StepBuilder` does not yet have an `AsScript` method (added in Task 2) or `ViaCliExecute` method. Add `ViaCliExecute()` to the step builder in `WorkflowBuilder.cs`:

```csharp
public StepBuilder ViaCliExecute()
{
    _step.ChannelOverride = ChannelType.CliExecute;
    return this;
}
```

**`src/KataFlow.Engine/Loaders/YamlWorkflowLoader.cs`** — ensure `channel_override` YAML value `"cli_execute"` maps to `ChannelType.CliExecute`:

```csharp
ChannelOverride = stepNode["channel_override"]?.AsString()?.ToLowerInvariant() switch
{
    "file_drop"  or "filedrop"  => ChannelType.FileDrop,
    "api_direct" or "apidirect" => ChannelType.ApiDirect,
    "cli_execute" or "cliexecute" => ChannelType.CliExecute,
    _ => null,
},
```

---

### Task 10 — CLI live progress display

**What:** Add a live Spectre.Console progress/status display to `RunCommand` that shows the current step, elapsed time, and running budget as steps execute. Replace the plain `Console.WriteLine` calls.

**Why:** Currently `RunCommand` just prints "Running workflow: X" and waits silently. For a workflow that takes 10 minutes, this is terrible UX.

**`src/KataFlow.Cli/Commands/RunCommand.cs`** — wrap the `runner.RunAsync` call with a Spectre.Console live display. The runner runs in a background task; the main thread updates the display by polling the session file every second.

Pattern:
```csharp
await AnsiConsole.Live(BuildStatusTable(def.Name, "Starting..."))
    .StartAsync(async ctx =>
    {
        var runTask = _runner.RunAsync(def, sessionCtx, cts.Token);

        while (!runTask.IsCompleted)
        {
            var session = await TryLoadSession(def.Name, _store);
            if (session is not null)
                ctx.UpdateTarget(BuildStatusTable(session));
            await Task.Delay(1000);
        }

        result = await runTask;
        ctx.UpdateTarget(BuildStatusTable(result));
    });
```

`BuildStatusTable` returns a `Spectre.Console.Table` with columns: Step | Status | Model | Tokens | Cost | Duration.

Add the session ID to the console output after `runner.RunAsync` starts so the user can use `kataflow watch --session <id>` or `kataflow approve --session <id>` from another terminal.

---

### Task 11 — Web UI fixes

**What:**  
1. Fix `ArtifactViewerComponent` to actually display artifact content (currently no `[content]` binding)  
2. Wire SignalR step-level events so the session detail page updates in real time  
3. Ensure the approval button in the session detail calls `POST /api/sessions/{id}/approve`  
4. Add budget summary to the session detail page  

**`src/KataFlow.Web/src/app/pages/sessions/session-detail.component.ts`** — the `ArtifactViewer` is called without `[content]`. Fix:

In `SessionDetailComponent`, after loading `detail`, fetch each artifact content:
```typescript
async loadArtifacts() {
  for (const artifact of this.detail?.artifacts ?? []) {
    const resp = await firstValueFrom(
      this.http.get<{ name: string; content: string }>(
        `/api/sessions/${this.sessionId}/artifacts/${artifact.name}`));
    artifact.content = resp.content;
  }
}
```

Update the template:
```html
<app-artifact-viewer *ngFor="let a of detail.artifacts" 
  [title]="a.name" 
  [content]="a.content ?? ''">
</app-artifact-viewer>
```

**`src/KataFlow.Web/src/app/components/artifact-viewer/artifact-viewer.component.ts`** — ensure the component accepts and renders a `@Input() content: string = ''` using a `<pre>` or markdown renderer.

**SignalR in session-detail:**

Add a `HubConnection` that joins the session group and listens for `StepCompleted` and `SessionCompleted` events from the API. On each event, refresh `detail` from `GET /api/sessions/{id}`.

**`src/KataFlow.Api/SessionHub.cs`** — add a `JoinSession` hub method:

```csharp
public async Task JoinSession(string sessionId)
{
    await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
}
```

**`src/KataFlow.Api/Endpoints/RunEndpoints.cs`** — after each step completes in the background task (you need to hook into the runner; simplest approach is to emit `StepCompleted` events from `WorkflowRunner` via a callback or event):

Add an `Action<StepResult>` callback to `SessionContext`:

```csharp
// In SessionContext:
public Action<StepResult>? OnStepCompleted { get; init; }
```

In `WorkflowRunner.RunAsync`, after a step succeeds:
```csharp
ctx.OnStepCompleted?.Invoke(stepResult);
```

In `RunEndpoints.Map`, set the callback:
```csharp
var ctx = new SessionContext
{
    ...,
    OnStepCompleted = stepResult =>
    {
        _ = hubContext.Clients.Group(session.Id).SendAsync("StepCompleted", new
        {
            sessionId = session.Id,
            stepResult.StepName,
            stepResult.Success,
            Budget = stepResult.Budget is not null ? new { stepResult.Budget.CostUsd, stepResult.Budget.InputTokens, stepResult.Budget.OutputTokens } : null
        });
    }
};
```

**Budget display in session-detail:**

Add a budget summary card below the steps timeline showing total cost, total tokens, and per-step breakdown table.

---

### Task 12 — Session management improvements

**What:**  
1. Add `kataflow list sessions` output with colors and budget  
2. Add `kataflow session delete --session <id>` CLI command  
3. Add `kataflow session clean` to delete all completed/failed sessions  

**`src/KataFlow.Cli/Commands/ListCommand.cs`** — update the sessions subcommand to display a Spectre.Console `Table` with columns: ID (truncated 8 chars) | Workflow | Status (colored) | Steps | Cost | Created | Duration.

Status color mapping:
- `Running` → yellow
- `WaitingApproval` → cyan
- `Complete` → green
- `Failed` → red
- `Cancelled` → dim

**New command: `kataflow session`** — add a `SessionCommand` class with two subcommands:
- `delete --session <id>` — calls `DELETE /api/sessions/{id}` or deletes the directory directly
- `clean` — deletes all sessions with status Complete, Failed, or Cancelled

Register in `Program.cs`.

---

### Task 13 — Add `.env.example`

**What:** Create `.env.example` at the repo root with all required and optional environment variables documented.

**`/.env.example`:**

```
# KataFlow environment configuration
# Copy this to .env and fill in your values.
# The .env file is gitignored and loaded automatically by the CLI.

# ── Required: at least one API key for headless mode ──────────────────────────

# Claude (Anthropic) API key — used by ClaudeAdapter in headless/api mode
ANTHROPIC_API_KEY=sk-ant-...

# DeepSeek API key — used by RestAdapter in headless/api mode
DEEPSEEK_API_KEY=sk-...

# OpenAI-compatible key (for any other REST endpoint)
OPENAI_API_KEY=

# ── Optional overrides ─────────────────────────────────────────────────────────

# Override the Claude model (default: claude-sonnet-4-6)
# KATAFLOW_CLAUDE_MODEL=claude-opus-4-8

# Override the Rest/DeepSeek model (default: deepseek-chat)
# KATAFLOW_REST_MODEL=deepseek-reasoner

# Override the CLI executor command (default: claude, mode: Stdin)
# KATAFLOW_CLI_COMMAND=opencode
# KATAFLOW_CLI_ARGUMENTS=run
# KATAFLOW_CLI_INPUT_MODE=Stdin

# Budget cap in USD — workflow will warn (not fail) if exceeded
# KATAFLOW_BUDGET_CAP=5.00
```

---

## 3. Implementation Order

Execute tasks in this order. Do not proceed to the next task until all specified tests pass.

```
Task 1  → CliExecuteChannel fix + appsettings update
Task 2  → Script agent type (depends on Task 1 for CliWrap package)
Task 3  → Budget tracking (independent; can run in parallel with Task 2 if desired)
Task 4  → WaitingApproval fix (depends on Task 3 for StepResult.Budget)
Task 5  → WatchCommand fix (independent)
Task 6  → ManualApprovalGate edit (depends on Task 3 for budget display)
Task 7  → API fixes (depends on Task 3 for budget endpoint)
Task 8  → Aspire AppHost fix (independent)
Task 9  → Standard workflows + templates (depends on Tasks 1, 2)
Task 10 → CLI live progress (depends on Task 4 for session status)
Task 11 → Web UI fixes (depends on Tasks 3, 7)
Task 12 → Session management (depends on Task 3 for budget in list)
Task 13 → .env.example (independent; do first or last)
```

---

## 4. Build & Test Requirements

Before submitting any task:

```powershell
dotnet build KataFlow.sln --no-incremental
dotnet test tests/KataFlow.Tests --no-build
```

Both must pass with zero errors and zero failing tests.

For each task, the new tests specified in that task must be present and passing. Do not delete or skip existing tests.

The Angular web app must compile:
```
cd src/KataFlow.Web && npm run build
```

---

## 5. What NOT to Change

- Do not change `KataFlow.Core` interfaces except as explicitly specified in tasks (adding properties to models is fine; changing method signatures is not).
- Do not change the YAML workflow format's existing keys — only add new ones (`script_command`, `channel_override`).
- Do not change the session JSON schema in a way that breaks existing sessions (add new nullable/optional fields only).
- Do not replace Spectre.Console with any other terminal library.
- Do not replace `System.CommandLine` with any other CLI parsing library.
- Do not add a database — session storage stays as JSON files.
- Do not change the template variable syntax (`{{var}}`) — it is intentional.
- Do not touch `KataFlow.AppHost/obj/` or `*/bin/` generated files.
- The `AddKataFlowGates()` extension must remain callable separately (CLI calls it; API does not call it because the web-based approval uses file signals, not Spectre.Console).

---

## 6. Key Configuration Reference

After Task 1, `appsettings.json` structure (both CLI and API):

```json
{
  "KataFlow": {
    "WorkflowsPath": "./workflows",
    "TemplatesPath": "./templates",
    "SessionsPath": "./sessions",
    "DefaultMode": "dev"
  },
  "Agents": {
    "Claude": {
      "ApiKey": "",
      "Model": "claude-sonnet-4-6",
      "MaxTokens": 16384,
      "FileDrop": { "WatchTimeoutMinutes": 15, "PollIntervalMs": 500 }
    },
    "Rest": {
      "ApiKey": "",
      "BaseUrl": "https://api.deepseek.com",
      "Model": "deepseek-chat",
      "MaxTokens": 16384,
      "FileDrop": { "WatchTimeoutMinutes": 30, "PollIntervalMs": 500 }
    },
    "CliExecute": {
      "Command": "claude",
      "Arguments": "--print",
      "InputMode": "Stdin",
      "TimeoutSeconds": 600
    }
  }
}
```

For OpenCode instead of Claude Code: change `Command` to `"opencode"`, `Arguments` to `"run"`.

User-level overrides go in `~/.kataflow/config.json` — same key structure, merged on top of `appsettings.json`.

---

## 7. Invoking the Completed System

**Running `agentic-dev` end-to-end with Claude Code as executor:**

```bash
# Set API key
export ANTHROPIC_API_KEY=sk-ant-...

# Prepare a codebase summary (you maintain this)
cat > ./codebase-summary.md << 'EOF'
[Brief description of the repo structure and conventions]
EOF

# Run
kataflow run --workflow agentic-dev \
  --var project="MyProject" \
  --var task="Add OAuth2 login support" \
  --context codebase=./codebase-summary.md \
  --var build_command="dotnet build --no-restore 2>&1" \
  --var test_command="dotnet test --no-build 2>&1"
```

**Approving from a second terminal (if running headless or out-of-band):**

```bash
kataflow approve --session <session-id>
kataflow run --session <session-id>
```

**Watching a running session:**

```bash
kataflow watch --session <session-id>
```

**Starting the web UI:**

```bash
cd src/KataFlow.AppHost && dotnet run
# Opens: http://localhost:4200 (Web) and https://localhost:17043 (Aspire dashboard)
```
