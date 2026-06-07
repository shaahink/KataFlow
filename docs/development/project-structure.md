# Project Structure

```
KataFlow/
├── KataFlow.slnx                          # .NET solution
├── Directory.Build.props                  # Shared build properties (TreatWarningsAsErrors)
├── .editorconfig                          # Code style rules
├── .gitattributes                         # Line ending normalization
│
├── src/
│   ├── KataFlow.Core/                     # Models, enums, interfaces, abstractions
│   ├── KataFlow.Engine/                   # WorkflowRunner, StepExecutor, Loaders, Gates
│   ├── KataFlow.Infrastructure/           # SessionStore, ArtifactStore, FileWatcher
│   ├── KataFlow.Adapters/                 # FileDrop, Claude, Rest, CliExecute channels
│   ├── KataFlow.ServiceDefaults/          # Shared DI extensions, workspace resolver
│   ├── KataFlow.Cli/                      # CLI commands (run, approve, status, list, watch)
│   ├── KataFlow.Api/                      # ASP.NET Minimal API + SignalR hub
│   ├── KataFlow.Web/                      # Angular SPA (Tailwind, d3, CodeMirror)
│   └── KataFlow.AppHost/                  # .NET Aspire orchestrator
│
├── tests/
│   └── KataFlow.Tests/                    # xUnit tests (unit + integration)
│
├── docs/                                  # Documentation
│   ├── README.md                          # Doc index
│   ├── architecture.md                    # System architecture
│   ├── spec.md                            # Original design specification
│   └── development/
│       ├── build-and-test.md              # Build and test instructions
│       └── project-structure.md           # This file
│
├── templates/                             # Prompt Markdown templates
│   ├── _system/                           # System templates (auto-appended)
│   ├── engineering/                       # Software engineering prompts
│   └── trading/                           # Trading strategy prompts
│
├── workflows/                             # YAML workflow definitions
│
├── .github/
│   ├── workflows/ci.yml                  # CI pipeline
│   ├── dependabot.yml                     # Automated dependency updates
│   └── pull_request_template.md           # PR template
│
├── start-dev.bat                          # Local dev startup script
└── .env.example                           # API key template
```

## Project Dependencies

```
KataFlow.Core          — no dependencies
KataFlow.Engine        → Core
KataFlow.Infrastructure → Core
KataFlow.Adapters      → Core, Infrastructure
KataFlow.ServiceDefaults → Core, Engine, Infrastructure, Adapters
KataFlow.Cli           → ServiceDefaults (transitively → everything)
KataFlow.Api           → ServiceDefaults (transitively → everything)
KataFlow.AppHost       → Api (Aspire orchestration)
KataFlow.Web           — standalone Angular project
KataFlow.Tests         → Core, Engine, Infrastructure, Adapters, Cli (for testing)
```
