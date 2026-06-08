# KataFlow

Multi-agent AI workflow orchestrator. Define pipelines of steps; each step is assigned to an agent (Claude, DeepSeek, OpenAI-compatible) or a shell command. The orchestrator handles prompt composition, context injection, agent communication, approval gating, budget tracking, and session state — with a CLI, REST API, and Angular web UI.

📚 **Full documentation:** [docs/README.md](docs/README.md) — architecture, spec, build & test guide.

## Quick start

```bash
# List available workflows
dotnet run --project src/KataFlow.Cli -- list workflows

# Run the software-lifecycle preset
dotnet run --project src/KataFlow.Cli -- run --workflow software-lifecycle \
  --var project=MyFeature --var goal="Add OAuth login"

# Run agentic-dev with Claude Code as executor
dotnet run --project src/KataFlow.Cli -- run --workflow agentic-dev \
  --var project=MyProject --var task="Add OAuth2 login" \
  --var build_command="dotnet build 2>&1" \
  --var test_command="dotnet test --no-build 2>&1" \
  --budget-cap 5.00

# List sessions
dotnet run --project src/KataFlow.Cli -- list sessions

# Watch a running session (live polling)
dotnet run --project src/KataFlow.Cli -- watch --session <session-id>

# Get session status with budget
dotnet run --project src/KataFlow.Cli -- status --session <session-id>

# Approve a pending step
dotnet run --project src/KataFlow.Cli -- approve --session <session-id>

# Session management
dotnet run --project src/KataFlow.Cli -- session delete --session <session-id>
dotnet run --project src/KataFlow.Cli -- session clean
```

## Web UI

Run both the API and Angular dev server with Aspire:

```bash
cd src/KataFlow.AppHost
dotnet run
```

Or separately:

```bash
# Terminal 1 — API
cd src/KataFlow.Api
dotnet run

# Terminal 2 — Web
cd src/KataFlow.Web
ng serve --proxy-config proxy.conf.json --port 4200
```

Then open **http://localhost:4200**.

### Web pages

| Page | Route | Description |
|---|---|---|
| Dashboard | `/` | Stats + recent sessions |
| Workflows | `/workflows` | Card grid of all presets, inline Run button |
| Workflow Editor | `/workflows/:name` | YAML editor + d3.js step graph |
| Templates | `/templates` | List of all Markdown templates |
| Template Editor | `/templates/:path` | Markdown editor + variable sidebar |
| Session Detail | `/sessions/:id` | d3.js pipeline + step timeline + budget + approval |

## Install as global tool

```bash
dotnet pack src/KataFlow.Cli -o ./nupkg
dotnet tool install --global --add-source ./nupkg KataFlow
kataflow list workflows
```

## Configuration

Copy `.env.example` to `.env` and add API keys for headless mode:

```
ANTHROPIC_API_KEY=sk-ant-...
DEEPSEEK_API_KEY=sk-...
OPENAI_API_KEY=
```

Config is loaded from `appsettings.json` (project default) merged with `~/.kataflow/config.json` (user override). Environment variables override config values.

### CliExecute configuration

You can switch the CLI executor between Claude Code and OpenCode:

```json
"CliExecute": {
  "Command": "opencode",     // or "claude"
  "Arguments": "run",        // or "--print"
  "InputMode": "Stdin"       // or "File"
}
```

## Agent types & channel modes

### Agent types

| Agent | Description |
|---|---|
| `Claude` | Anthropic Claude via API or FileDrop |
| `Rest` | DeepSeek / OpenAI-compatible via API or CliExecute |
| `Script` | Shell command executed directly via CliWrap (no LLM) |

### Channel modes

| Channel | Mode | Description |
|---|---|---|
| `FileDrop` | Dev | Writes task file → agent reads → watcher detects output |
| `CliExecute` | Dev | Pipes prompt via stdin or temp file → spawns CLI → captures stdout |
| `ApiDirect` | Headless | Calls agent REST API directly (Claude / DeepSeek) |

## Budget tracking

Every step tracks input/output tokens and estimated USD cost. The CLI displays cost in:

- Live progress table (Spectre.Console)
- `kataflow status --session <id>`
- `kataflow list sessions`
- `kataflow watch --session <id>`

Supported model pricing (hardcoded June 2026 rates):

| Model | Input $/1M | Output $/1M |
|---|---|---|
| claude-sonnet-4-6 | 3.00 | 15.00 |
| claude-haiku-4-5-20251001 | 0.80 | 4.00 |
| claude-opus-4-8 | 15.00 | 75.00 |
| deepseek-chat | 0.14 | 0.28 |
| deepseek-reasoner | 0.55 | 2.19 |
| gpt-4o | 2.50 | 10.00 |

Set a budget cap with `--budget-cap 5.00` — the runner warns (does not fail) if exceeded.

## Approval gates

| Gate | Behavior |
|---|---|
| `AutoApprovalGate` | Auto-approves all steps (registered in API and CLI) |
| `ManualApprovalGate` | Interactive Spectre.Console prompt with **Approve** / **Reject** / **View** / **Edit** options |

The **Edit** option opens the artifact in `$EDITOR` (or Notepad on Windows), waits for save, re-reads the file, and approves.

## Workflows

### Baked-in presets

| Name | Steps | Description |
|---|---|---|
| `software-lifecycle` | plan → implement → review → report | Full SE lifecycle |
| `agentic-dev` | plan → implement → build → test → review → report | Agentic programming with Claude Code executor |
| `bug-fix` | diagnose → fix → verify → report | Bug diagnosis and fix pipeline |
| `code-review-agentic` | review → report | Review an implementation artifact |
| `trading-strategy` | generate → test → review → report | Strategy development |
| `planner-only` | plan | Planning only |
| `review-only` | review | Review an injected artifact |
| `quick-execute` | implement | Single-step executor handoff |

Custom YAML workflows go in `./workflows/`.

### YAML example (with Script step)

```yaml
workflow:
  name: my-workflow
  default_mode: dev
  variables:
    build_command: "dotnet build 2>&1"
    test_command: "dotnet test --no-build 2>&1"
  steps:
    - name: plan
      agent: claude
      role: planner
      prompt_template: templates/engineering/planner.md
      approval: manual
      output_artifact: plan

    - name: build
      agent: script
      role: verifier
      script_command: "{{build_command}}"
      approval: auto
      output_artifact: build-output

    - name: implement
      agent: rest
      role: executor
      channel_override: cli_execute
      prompt_template: templates/agentic/executor.md
      context_artifacts: [plan]
      approval: manual
      output_artifact: implementation
```

## CLI reference

```bash
kataflow run       --workflow <name> [--session <id>] [--mode dev|headless]
                   [--auto-approve] [--var key=value] [--context name=path]
                   [--budget-cap <usd>]
kataflow approve   --session <id> [--reject]
kataflow status    [--session <id>] [--json]
kataflow list      workflows|sessions [--json]
kataflow watch     --session <id>
kataflow session   delete --session <id>
kataflow session   clean
```

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│  CLI (KataFlow.Cli)    API (KataFlow.Api)   Web (Angular)│
│  System.CommandLine    ASP.NET Minimal API   Tailwind CSS │
│  Spectre.Console Live   SignalR + JSON       SignalR     │
└────────────────────────┬─────────────────────────────────┘
                         │
┌────────────────────────▼─────────────────────────────────┐
│  Engine (KataFlow.Engine)                                 │
│  WorkflowRunner → StepExecutor → SessionManager           │
│  ContextBuilder · PromptRenderer · ScriptStep             │
│  AutoApprovalGate · ManualApprovalGate (Edit + Budget)    │
└────────────────────────┬─────────────────────────────────┘
                         │
┌────────────────────────▼─────────────────────────────────┐
│  Adapters (KataFlow.Adapters)                             │
│  FileDropChannel  ClaudeApiChannel  RestApiChannel        │
│  CliExecuteChannel (Stdin + File modes)                   │
└────────────────────────┬─────────────────────────────────┘
                         │
┌────────────────────────▼─────────────────────────────────┐
│  Core (KataFlow.Core)                                     │
│  Models · Enums · Budget · ModelPricing                  │
└────────────────────────┬─────────────────────────────────┘
                         │
┌────────────────────────▼─────────────────────────────────┐
│  Infrastructure (KataFlow.Infrastructure)                 │
│  SessionStore · ArtifactStore · FileWatcher · SystemFile  │
└──────────────────────────────────────────────────────────┘
```

## Aspire Dashboard

When running via `KataFlow.AppHost`, the Aspire Dashboard provides:

- Live traces and structured logs
- Resource monitoring (API + Web)
- OTLP endpoint for OpenTelemetry

Access at `https://localhost:17043` (login token printed in console).

## Testing

```bash
# .NET unit tests (fast)
dotnet test --filter "Category=Unit"

# .NET integration tests (file system)
dotnet test --filter "Category=Integration"

# Playwright E2E tests (API + Angular)
cd src/KataFlow.Web
npx playwright test
```

## Templates

Templates are Markdown files with `{{variable}}` substitution.

| Directory | Contents |
|---|---|
| `templates/_system/` | System-level instructions (output path injection) |
| `templates/engineering/` | Software lifecycle templates (planner, executor, reviewer, reporter) |
| `templates/agentic/` | Agentic programming templates (planner, executor, reviewer, reporter, diagnoser, fixer) |
| `templates/trading/` | Trading strategy templates |
