---
id: BL-1191
title: Refuse --fault and --control beside mcp+stdio
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1155]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-060
created: 2026-11-15
completed: 2026-11-15
---
# BL-1191 — Refuse --fault and --control beside mcp+stdio

## Goal

`surl mcp+stdio: --fault ...` and `surl mcp+stdio: --control ...` are refused `FailedInit` (2) with a line saying why, instead of being accepted and silently ignored (ADR-0162 decision 3).

## Context

- ADR-0162 decides mcp+stdio stays outside the event stream and fault rules. `McpStdioComposition.FindRefusal` today refuses extra listen URLs, proxy listen URLs and `-` trace/log files, but not `SurlCommandLine.Faults` or `SurlCommandLine.ControlListenUrl`, so a test that asks for them gets no fault and no control endpoint, with no warning.

## Acceptance criteria

- [x] `McpStdioComposition.FindRefusal` returns `FailedInit` with a line starting `mcp+stdio:` and naming `--fault` when `Faults` is not empty.
- [x] It does the same naming `--control` when `ControlListenUrl` is set.
- [x] Tests in `McpStdioCompositionTests` cover both refusals and a command line with neither (still `null`).
- [x] The `mcp` topic's about text in `--aihelp` says `mcp+stdio:` takes neither option, and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

Decision: refusal text names the option and says the session is outside the event stream (ADR-0162 decision 3). Faults checked before control.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. mcp+stdio refuses --fault and --control
