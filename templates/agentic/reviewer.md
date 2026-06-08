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
