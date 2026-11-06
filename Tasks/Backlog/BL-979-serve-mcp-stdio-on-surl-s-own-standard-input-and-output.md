---
id: BL-979
title: Serve mcp+stdio on surl's own standard input and output
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-978, BL-467]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-979 — Serve mcp+stdio on surl's own standard input and output

## Goal

`surl mcp+stdio:<as ADR-0111 decision 5 spells it>` serves one MCP session on surl's own standard input and output,
with logging on standard error only, and the extension switch turns it off.

## Context

- Second of the two tasks BL-468 was split into (see BL-978).
- Decision: ADR-0111 decision 5 (stdio's command-line shape and whether it combines with other URLs).
- The console's standard streams are given to BL-467's session through an injected seam, so tests use in-memory
  streams. Check that no log line reaches standard output while stdio is served (`Application/Surl.Console/LogStreams.cs`).
- `--aihelp`: the `mcp` topic's `HelpCategories` row gains `mcp+stdio`, and its paragraph and an example describe
  the stdio form (`CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`).
- Stand-in: no shared store; one session per process.

## Acceptance criteria

- [ ] A named test finds `mcp+stdio` registered when the switch is on, and absent when it is off.
- [ ] A named test runs `surl` with the stdio URL over in-memory standard streams, answers `initialize`, and finds
      standard output holding only MCP messages, even with `--verbose`.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition`, `Surl.Console` and
      `Surl.Cli` keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
