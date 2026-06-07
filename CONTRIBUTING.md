# Contributing

## Branch Strategy

```
main          ─── Production-ready code, merged via PR only
feat/*        ─── Features branches from main (feat/cli-execute, feat/web-ui)
fix/*         ─── Bug fixes from main (fix/session-store-path)
refactor/*    ─── Refactoring (refactor/service-defaults)
chore/*       ─── Maintenance, CI, docs (chore/ci-cache, chore/readme)
```

- All branches are created from `main` and merged back via squash-merge
- Branch names use kebab-case: `feat/new-feature`, `fix/bug-description`
- After merge, delete the feature branch (both local and remote)

## Commit Messages

Follow conventional commits:

```
feat:     New feature (adds functionality)
fix:      Bug fix
refactor: Code change without feature/fix
chore:    Maintenance, CI, dependencies
docs:     Documentation only
test:     Test additions or changes
```

Examples:
```
feat: CliWrap integration for automated CLI agent execution
fix: resolve workspace root in API endpoint paths
refactor: extract shared DI into ServiceDefaults project
chore: add caching to CI workflow
docs: update README with web UI setup
```

## Pull Request Checklist

- [ ] Builds clean (`dotnet build`)
- [ ] Unit tests pass (`dotnet test --filter Category=Unit`)
- [ ] Integration tests pass (`dotnet test --filter Category=Integration`)
- [ ] No new warnings
- [ ] Tests added for new/changed behaviour
- [ ] README updated if user-facing

## Code Style

- .editorconfig rules are enforced (indent 4 spaces for C#, 2 for YAML/JSON)
- TreatWarningsAsErrors is enabled — no warnings allowed
- Follow existing patterns (file-scoped namespaces, primary constructors, records where appropriate)
