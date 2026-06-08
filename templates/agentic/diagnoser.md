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
