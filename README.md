# KataFlow

Multi-agent AI workflow orchestrator. Define pipelines of steps; each step is assigned to an agent (Claude, DeepSeek, OpenAI-compatible). The orchestrator handles prompt composition, context injection, agent communication, approval gating, and session state.

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

## Install as global tool

```bash
dotnet pack src/KataFlow.Cli
dotnet tool install --global --add-source src/KataFlow.Cli/nupkg KataFlow
kataflow list workflows
```

## Configuration

Copy `.env.example` to `.env` and add API keys for headless mode:

```
ANTHROPIC_API_KEY=sk-ant-...
DEEPSEEK_API_KEY=sk-...
```

Config is loaded from `appsettings.json` (project default) merged with `~/.kataflow/config.json` (user override).

## Workflows

Five baked-in presets:

| Name | Steps | Description |
|---|---|---|
| `software-lifecycle` | plan → implement → review → report | Full SE lifecycle |
| `trading-strategy` | generate → test → review → report | Strategy development |
| `planner-only` | plan | Planning only |
| `review-only` | review | Review an injected artifact |
| `quick-execute` | implement | Single-step executor handoff |

Custom YAML workflows go in `./workflows/`.

## JSON output

```bash
kataflow list workflows --json
kataflow status --json
```

## Architecture

```
CLI Layer    → Engine Layer    → Adapters Layer
kataflow run     WorkflowRunner     FileDropChannel (dev)
kataflow status  StepExecutor       ClaudeApiChannel (headless)
kataflow list    SessionManager     RestApiChannel (headless)
```
