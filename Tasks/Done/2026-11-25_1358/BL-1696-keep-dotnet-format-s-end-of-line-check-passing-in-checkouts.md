---
id: BL-1696
title: Keep dotnet format's end-of-line check passing in checkouts where tools write LF files
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [.gitattributes, .claude/hooks, .claude/settings.json, .claude/skills/verify]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1696 — Keep dotnet format's end-of-line check passing in checkouts where tools write LF files

## Goal

A `.cs` file an agent writes with LF line endings does not make `dotnet format --verify-no-changes` fail in that
checkout.

## Context

BL-1693 and BL-1694, 2026-11-25. `.editorconfig` asks for `end_of_line = crlf`, `.gitattributes` has `* text=auto`
and Stewart's machine has `core.autocrlf true`, so the index holds LF and a fresh Windows checkout holds CRLF and
passes. A file written with LF stays LF in that working tree until git rewrites it, and the check reports `ENDOFLINE`
on it (`git ls-files --eol` shows `i/lf w/lf`). Candidates: a hook that writes CRLF after an edit, a step in the
`verify` skill, or `*.cs text eol=crlf` in `.gitattributes`. The last one also changes Linux and macOS checkouts, and
with them the line breaks inside C# raw and verbatim string literals there, so it needs a green CI run on all three.

## Acceptance criteria

- [x] In a checkout holding a tracked `.cs` file rewritten with LF endings, the documented verify steps pass or turn it into CRLF; the Notes show the commands and their output.
- [x] The chosen fix and the reason for it are recorded in the Notes.
- [x] If `.gitattributes` changes, the `CI` workflow passes on Windows, Linux and macOS for the commit. (`.gitattributes` did not change, so no run is needed.)

## Notes

### Chosen fix (decided by Claude under Stewart's delegation, 2026-11-25)

Step 1 of the `verify` skill now runs `.claude/skills/verify/convert-lf-cs-files-to-crlf.ps1`, which rewrites every
tracked, or untracked and not ignored, `.cs` file holding LF to CRLF. Files marked `-text` are skipped, and only line
breaks change. A bare `dotnet format --verify-no-changes` run without that step still reports `ENDOFLINE` on such a file.

Why:

- It covers every way a file gets written (Edit, Write, Bash, PowerShell, any other tool), at the one point where the
  check runs.
- It runs before the build, so the build and tests read the same bytes (line breaks inside raw and verbatim string
  literals included) as a fresh Windows checkout.
- Not a hook: `.claude/hooks/format-cs.ps1` already turns an LF file into CRLF after `Edit` and `Write` (checked here:
  `i/lf w/lf` before running it on one file, `i/lf w/crlf` after). A second hook after every `Bash` and `PowerShell`
  call would cost time on each call and rewrite files behind a running session.
- Not `*.cs text eol=crlf`: it acts only when git checks a file out, so it does not change a file a tool has just
  written, and it would change Linux and macOS checkouts.
- No ADR: this changes how the repository is verified, not Surl's design or behaviour.

### Evidence (lane 1 worktree)

`Foundation/Surl.Core.UnitLibrary/ConnectionAdmission.cs` rewritten with LF:

```
> git ls-files --eol -- $f
i/lf    w/lf    attr/text=auto  Foundation/Surl.Core.UnitLibrary/ConnectionAdmission.cs
> dotnet format --verify-no-changes --no-restore --include $f
exit=2, 86 "error ENDOFLINE" lines
> powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/verify/convert-lf-cs-files-to-crlf.ps1    (step 1)
CRLF: Foundation/Surl.Core.UnitLibrary/ConnectionAdmission.cs
1 C# file(s) rewritten from LF to CRLF.
> git ls-files --eol -- $f
i/lf    w/crlf  attr/text=auto  Foundation/Surl.Core.UnitLibrary/ConnectionAdmission.cs
> dotnet restore                                    (step 2) exit=0
> dotnet build --no-restore -warnaserror            (step 3) exit=0, 0 Warning(s), 0 Error(s)
> dotnet format --verify-no-changes                 (step 4) exit=0, whole solution
> dotnet test --no-build --filter "TestCategory!=Integration"    (step 5) exit=0, 83 assemblies, 28945 passed, 0 failed, 3 skipped
> git status --short                                the .cs file is not listed
```

In a first run, the rewritten file's SHA-256 matched its checkout bytes after step 1, and an untracked file with a
byte-order mark, a non-ASCII character and mixed endings went from `EF BB BF 61 0A C3 A9 0D 0A 62 0A` to
`EF BB BF 61 0D 0A C3 A9 0D 0A 62 0D 0A`.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The verify skill's first step turns LF .cs files into CRLF, so dotnet format --verify-no-changes passes in a checkout where tools wrote LF
