---
name: verify
description: Full pre-commit verification for the Surl solution — restore, build with warnings as errors, format check, and tests. Use before committing, when asked to verify, check, or validate the solution, or at the end of any multi-file change.
---
# Verify the solution

Run in order from the repository root; stop and fix at the first failure.

1. `powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/verify/convert-lf-cs-files-to-crlf.ps1`
   — rewrites every `.cs` file that a tool wrote with LF (tracked, or untracked and not ignored) to CRLF, as
   `.editorconfig` asks and a fresh Windows checkout holds it. It runs first so the build, the format check and the
   tests all read the same bytes. `.gitattributes` (`* text=auto`) stores LF either way, so a file whose only change
   was its line endings leaves `git diff` empty: nothing to commit and no task to file.
2. `dotnet restore`
3. `dotnet build --no-restore -warnaserror`
4. `dotnet format --verify-no-changes` — if it fails, run `dotnet format` and report which files changed.
5. `dotnet test --no-build --filter "TestCategory!=Integration"`
6. If the change touched integration-level code (network, file system, database), also run `dotnet test --no-build --filter "TestCategory=Integration"`.

Report a short summary: build status, warning count, tests passed/failed/skipped, and files reformatted.
