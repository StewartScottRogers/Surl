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
completed:
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

- [ ] The `--auth` Explanation names every method `AuthenticationComposition` composes, and no
      sentence in it is false of the code.
- [ ] `ManualText.cs`'s exit-code 2 line names only refusals the code can make.
- [ ] The `Surl.Cli.UnitTests` tests that pin those texts are updated and green; `dotnet build`
      clean; fast tests green.

## Notes

## Log

- 2026-09-29: Created.
