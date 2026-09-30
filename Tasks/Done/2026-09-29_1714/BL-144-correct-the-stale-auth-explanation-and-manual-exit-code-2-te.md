---
id: BL-144
title: Correct the stale --auth explanation and manual exit-code 2 text now that aws-sigv4 is checked
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: FR-008
created: 2026-09-29
completed: 2026-09-29
---
# BL-144 — Correct the stale --auth explanation and manual exit-code 2 text now that aws-sigv4 is checked

## Goal

`surl --help --auth` and `surl --manual` say which `--auth` methods this build checks, truly,
now that `Surl.Console/AuthenticationComposition.cs` composes all six (`negotiate`, `ntlm`,
`digest`, `basic`, `bearer`, `aws-sigv4`).

## Context

Found in BL-137 (ADR-0046) on 2026-09-29. The `--auth` Explanation in
`Surl.Cli.UnitLibrary/CommandLineOptions.cs` (lines 37-38) says "This build checks basic,
bearer, digest, ntlm and negotiate, and refuses to start when --auth names another", but
`AuthenticationComposition.cs` also maps `aws-sigv4` (BL-136). `ManualText.cs`'s `EXIT CODES`
line for 2 ("an --auth method this build does not have") may be stale for the same reason:
check whether any `--auth` word can still reach that refusal, and word the line to match.
ADR-0046 decision 6 leaves that clause out of exit code 2's guidance until this is settled;
BL-138 rechecks every meaning against the code.

## Acceptance criteria

- [x] The `--auth` Explanation names every method `AuthenticationComposition` composes, and no
      sentence in it is false of the code.
- [x] `ManualText.cs`'s exit-code 2 line names only refusals the code can make.
- [x] The `Surl.Cli.UnitTests` tests that pin those texts are updated and green; `dotnet build`
      clean; fast tests green.

## Notes

- 2026-09-29: BL-145 (commit 66d2e3b) already made both edits: the `--auth` Explanation in `CommandLineOptions.cs` now names all six methods `AuthenticationComposition` composes and says surl checks every one, refusing any other word as an option badly used (exit code 2); `ManualText.cs`'s exit-code 2 line dropped the "--auth method this build does not have" clause. Checked: `OptionArgumentReader` accepts exactly the six words `AuthenticationComposition.MethodsByWord` maps, so no `--auth` word can reach a missing-method refusal; an unknown word is "an option refused". `HelpTextTests` and `ManualTextTests` pin the new text. No code change needed in this task; build clean, fast tests green (Surl.Cli.UnitTests 639 passed).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --help --auth and --manual truly name the six checked --auth methods and exit-code 2's refusals (text landed in BL-145, verified here)
