# Build & Test

## Prerequisites

- .NET 10 SDK
- Node.js 22+
- Angular CLI 19 (`npm install -g @angular/cli@19`)

## Build

```bash
# Full solution
dotnet build

# Specific project
dotnet build src/KataFlow.Api/KataFlow.Api.csproj

# Release configuration
dotnet build --configuration Release
```

## Test

```bash
# .NET unit tests (fast, no external dependencies)
dotnet test --filter "Category=Unit"

# .NET integration tests (file system)
dotnet test --filter "Category=Integration"

# All .NET tests
dotnet test
```

## E2E Tests

Playwright E2E tests run against the real API and Angular dev server:

```bash
cd src/KataFlow.Web

# Install Playwright browsers (first time only)
npx playwright install chromium

# Run all E2E tests
npx playwright test

# Run specific tests
npx playwright test --grep "smoke"

# Interactive mode
npx playwright test --ui
```

The Playwright config (`playwright.config.ts`) automatically starts:
1. The .NET API on port 5100
2. Angular dev server on port 4200

Both servers shut down when tests complete.

## CI Pipeline

GitHub Actions runs three jobs in sequence:

| Job | Filter | Purpose |
|---|---|---|
| `dotnet-build-and-test` | `Category=Unit` | Fast feedback |
| `integration-tests` | `Category=Integration` | File system tests |
| `e2e-tests` | All Playwright | Full stack E2E |

Caching is enabled for NuGet packages and npm modules.
