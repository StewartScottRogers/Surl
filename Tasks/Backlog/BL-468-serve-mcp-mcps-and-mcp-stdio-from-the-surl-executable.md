---
id: BL-468
title: Serve mcp, mcps and mcp+stdio from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-466, BL-467, BL-405]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-468 — Serve mcp, mcps and mcp+stdio from the surl executable

## Goal

`surl mcp://127.0.0.1:0` serves MCP over Streamable HTTP, and `surl mcps://127.0.0.1:0` serves it over TLS.
`surl mcp+stdio:<as the ADR spells it>` serves one session on surl's own standard input and output, with
logging on standard error only. The `--aihelp` topic and help category exist, and the extension switch turns all
three off.

## Context

- Decision: BL-463's ADR, decision 5 (stdio's command-line shape and whether it combines with other URLs).
- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`. `mcps`
  is wrapped for TLS (`ImplicitTlsSchemeServer`, ADR-0010). For stdio, the console's standard streams are given
  to BL-467's session through an injected seam, so tests use in-memory streams. Check that no log line
  reaches standard output while stdio is served (`LogStreams`).
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with the three schemes, an
  `AiHelpProse.TopicAbout` paragraph naming MCP 2025-06-18 as its oracle, an `AiHelpExamples` entry, and the
  topic list in `AiHelpTextTests` growing by one.

## Acceptance criteria

- [ ] A named test finds `mcp`, `mcps` and `mcp+stdio` registered when the switch is on, and absent when it is off.
- [ ] A named test runs `surl` with the stdio URL over in-memory standard streams, answers `initialize`, and finds
      standard output holding only MCP messages, even with `--verbose`.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
