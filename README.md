# KataFlow

Multi-agent AI workflow orchestrator. Define pipelines of steps; each step is assigned to an agent (Claude, DeepSeek, OpenAI-compatible). The orchestrator handles prompt composition, context injection, agent communication, approval gating, and session state — with a CLI, REST API, and Angular web UI.

## Quick start

```bash
# List available workflows
dotnet run --project src/KataFlow.Cli -- list workflows

# Run the software-lifecycle preset
dotnet run --project src/KataFlow.Cli -- run --workflow software-lifecycle \
  --var project=MyFeature --var goal="Add OAuth login"

# List sessions
dotnet run --project src/KataFlow.Cli -- list sessions

# Get session status
dotnet run --project src/KataFlow.Cli -- status --session <session-id>

# Approve a pending step
dotnet run --project src/KataFlow.Cli -- approve --session <session-id>
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
| Session Detail | `/sessions/:id` | d3.js pipeline + step timeline + approval |

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
```

Config is loaded from `appsettings.json` (project default) merged with `~/.kataflow/config.json` (user override).

## Channel modes

| Channel | Mode | Description |
|---|---|---|
| `FileDrop` | Dev | Writes task file → agent reads → watcher detects output |
| `CliExecute` | Dev | Writes input file → spawns CLI command → captures stdout |
| `ApiDirect` | Headless | Calls agent REST API directly (Claude / DeepSeek) |

## Workflows

### Baked-in presets

| Name | Steps | Description |
|---|---|---|
| `software-lifecycle` | plan → implement → review → report | Full SE lifecycle |
| `trading-strategy` | generate → test → review → report | Strategy development |
| `planner-only` | plan | Planning only |
| `review-only` | review | Review an injected artifact |
| `quick-execute` | implement | Single-step executor handoff |

Custom YAML workflows go in `./workflows/`.

### YAML example

```yaml
workflow:
  name: my-workflow
  default_mode: dev
  steps:
    - name: plan
      agent: claude
      role: planner
      prompt_template: templates/engineering/planner.md
      approval: manual
      output_artifact: plan
```

## CLI reference

```bash
kataflow run       --workflow <name> [--mode dev|headless] [--auto-approve] [--var key=value]
kataflow approve   --session <id> [--reject]
kataflow status    [--session <id>] [--json]
kataflow list      workflows|sessions [--json]
kataflow watch     --session <id>
```

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│  CLI (KataFlow.Cli)    API (KataFlow.Api)   Web (Angular)│
│  System.CommandLine    ASP.NET Minimal API   Tailwind CSS │
└────────────────────────┬─────────────────────────────────┘
                         │
┌────────────────────────▼─────────────────────────────────┐
│  Engine (KataFlow.Engine)                                 │
│  WorkflowRunner → StepExecutor → SessionManager           │
│  ContextBuilder · PromptRenderer · ApprovalGates          │
└────────────────────────┬─────────────────────────────────┘
                         │
┌────────────────────────▼─────────────────────────────────┐
│  Adapters (KataFlow.Adapters)                             │
│  FileDropChannel  ClaudeApiChannel  RestApiChannel        │
│  CliExecuteChannel                                        │
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
