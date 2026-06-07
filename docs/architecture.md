# Architecture

## Overview

```
┌──────────────────────────────────────────────────────────┐
│  CLI              API                Web                  │
│  KataFlow.Cli     KataFlow.Api       KataFlow.Web         │
│  System.CommandLine  ASP.NET Minimal  Angular 19          │
│                     API + SignalR     Tailwind CSS        │
│                                        d3.js + CodeMirror │
└───────────────────────┬──────────────────────────────────┘
                        │
┌───────────────────────▼──────────────────────────────────┐
│  Engine (KataFlow.Engine)                                 │
│  WorkflowRunner → StepExecutor → SessionManager           │
│  ContextBuilder · PromptRenderer · ApprovalGates          │
└───────────────────────┬──────────────────────────────────┘
                        │
┌───────────────────────▼──────────────────────────────────┐
│  Adapters (KataFlow.Adapters)                             │
│  FileDropChannel     ClaudeApiChannel   RestApiChannel    │
│  CliExecuteChannel                                        │
└───────────────────────┬──────────────────────────────────┘
                        │
┌───────────────────────▼──────────────────────────────────┐
│  Infrastructure (KataFlow.Infrastructure)                 │
│  SessionStore · ArtifactStore · FileWatcher · SystemFile  │
└──────────────────────────────────────────────────────────┘
```

## Layers

### Core (`KataFlow.Core`)

Zero-dependency project containing:
- **Models**: `WorkflowDefinition`, `StepDefinition`, `Session`, `AgentRequest/Response`, `SessionResult`
- **Enums**: `AgentType`, `ChannelType`, `ApprovalMode`, `OrchestratorMode`, `SessionStatus`, `StepType`
- **Interfaces**: `IAgentAdapter`, `IAgentChannel`, `IWorkflowRunner`, `IWorkflowLoader`, `IApprovalGate`, `ISessionStore`, `IArtifactStore`, `IPromptRenderer`
- **Abstractions**: `IFileSystem` — file I/O abstraction for testability
- **Constants**: Centralized strings (env var names, paths, signal file names)

### Engine (`KataFlow.Engine`)

Business logic and workflow orchestration:
- **WorkflowRunner**: Session lifecycle → step iteration → approval gates
- **StepExecutor**: Single-step execution with retry and timeout
- **SessionManager**: Session create/resume/persist lifecycle
- **ContextBuilder**: Variable resolution (env → session → artifacts → system)
- **PromptRenderer**: `{{var}}` template substitution
- **WorkflowBuilder**: Fluent API for C# preset definitions
- **Loaders**: `PresetWorkflowRegistry` (baked-in), `YamlWorkflowLoader` (file-based), `CompositeWorkflowLoader` (preset-first, then YAML)
- **Gates**: `AutoApprovalGate` (immediate), `ManualApprovalGate` (interactive + file signal)

### Infrastructure (`KataFlow.Infrastructure`)

File-system backed persistence:
- **SessionStore**: JSON file-backed session CRUD
- **ArtifactStore**: Step output persistence
- **FileWatcher**: `FileSystemWatcher` + poll fallback for FileDrop channel
- **SystemFileSystem**: `IFileSystem` implementation wrapping `System.IO`

### Adapters (`KataFlow.Adapters`)

Agent communication channels:

| Channel | Mode | Transport | Use Case |
|---|---|---|---|
| `FileDropChannel` | Dev | File system | Write task → agent reads → watcher detects output |
| `CliExecuteChannel` | Dev | CliWrap | Write prompt → spawn CLI → capture stdout |
| `ClaudeApiChannel` | Headless | HTTP REST | Anthropic Messages API |
| `RestApiChannel` | Headless | HTTP REST | OpenAI-compatible (DeepSeek, etc.) |

Adapters resolve channels at runtime: `Dev` mode defaults to `FileDrop`, `Headless` defaults to `ApiDirect`. Steps can override with `channelOverride`.

### API (`KataFlow.Api`)

ASP.NET Core Minimal API — reuses Engine via shared `KataFlow.ServiceDefaults` DI:

| Endpoint | Description |
|---|---|
| `GET/POST/PUT/DELETE /api/workflows` | CRUD YAML workflows |
| `GET/PUT /api/templates` | Read/edit Markdown templates |
| `GET /api/templates/{**path}/variables` | Extract `{{var}}` from template |
| `GET /api/sessions`, `GET /api/sessions/{id}` | Session querying |
| `POST /api/sessions/{id}/approve` | Approve/reject step |
| `POST /api/runs` | Start workflow execution (async) |
| `DELETE /api/sessions/{id}` | Cleanup |
| SignalR `/hubs/session` | Real-time step updates |

### Web (`KataFlow.Web`)

Angular 19 SPA with lazy-loaded routes:

| Route | Page |
|---|---|
| `/` | Dashboard — stats cards + recent sessions |
| `/workflows` | Workflow card grid with Run button |
| `/workflows/:name` | YAML CodeMirror editor + d3.js graph preview |
| `/templates` | Template list |
| `/templates/:path` | Markdown editor + variable sidebar |
| `/sessions/:id` | d3.js pipeline + step timeline + approval |
| `/settings` | Configuration display |

### AppHost (`KataFlow.AppHost`)

.NET Aspire orchestration — starts API + Web together with:
- Aspire Dashboard at `https://localhost:17043`
- OpenTelemetry tracing via console exporter

## Communication Channels

### FileDrop (Dev mode)

```
Orchestrator                     Agent (Claude Code / OpenCode)
──────────                       ──────────────────────────────
Write task ──────────────────►   sessions/{id}/task-{step}.md
                                   (agent reads, works...)

FSW event ◄────────────────────   sessions/{id}/output-{step}.md
Read output → validate → store
```

### CliExecute (Automated Dev)

```
Orchestrator
──────────
Write prompt to input-{step}.md
Spawn: opencode --prompt "input-{step}.md"
Capture stdout → parse → store
```

### ApiDirect (Headless mode)

```
POST {base}/v1/messages  ───►   Claude / DeepSeek API
      └ model, messages[user]     └ content[text]
Response ◄─────────────────────
```

## Variable Resolution

Priority (highest first):
1. Reserved system vars (`_session_id`, `_step_name`, `_workflow_name`, `_output_path`)
2. Context artifacts from prior steps (`{{plan}}`, `{{implementation}}`)
3. Session variables (`--var key=value`)
4. Workflow-level variables (YAML `variables:` block)
5. Environment variables (excluding known API keys)
