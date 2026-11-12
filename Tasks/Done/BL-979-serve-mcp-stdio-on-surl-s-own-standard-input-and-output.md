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
completed: 2026-11-12
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

- [x] A named test finds `mcp+stdio` registered when the switch is on, and absent when it is off.
- [x] A named test runs `surl` with the stdio URL over in-memory standard streams, answers `initialize`, and finds
      standard output holding only MCP messages, even with `--verbose`.
- [x] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition`, `Surl.Console` and
      `Surl.Cli` keep 100% line and branch coverage.

## Notes

- Choice: `mcp+stdio` is a registration (`McpStdioListenUrlServer` claims the scheme, never serves) so `SURL_EXTENSIONS=0` and the parser's claimed-scheme list work unchanged; `ListenUrlParser` reads `mcp+stdio:` and `mcp+stdio://` apart (empty host, port 0, no `ExtendedSchemeDefaultPorts` row). `McpStdioComposition` serves it, outside the engine, over the runner's stream seams.
- Choice: the `--aihelp` example for the stdio form is a refusal (beside another listen URL), since a serving example would read real standard input.
- Choice: `--directory` and exposure options apply to the content store; no fault plan or event stream is composed for stdio (one session per process, no shared store).

## Log

- 2026-11-06: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. surl mcp+stdio: serves one MCP session on standard input and output, log on stderr only, switch turns it off
