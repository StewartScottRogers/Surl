---
name: verify
description: Full pre-commit verification for the Surl solution — restore, build with warnings as errors, format check, and tests. Use before committing, when asked to verify, check, or validate the solution, or at the end of any multi-file change.
---
# Verify the solution

Run in order from the repository root; stop and fix at the first failure.

1. `dotnet restore`
2. `dotnet build --no-restore -warnaserror`
3. `dotnet format --verify-no-changes` — if it fails, run `dotnet format` and report which files changed.
4. `dotnet test --no-build --filter "TestCategory!=Integration"`
5. If the change touched integration-level code (network, file system, database), also run `dotnet test --no-build --filter "TestCategory=Integration"`.

Report a short summary: build status, warning count, tests passed/failed/skipped, and files reformatted.
