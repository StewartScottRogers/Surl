---
id: BL-2111
title: Gate the Surl.Mcp executable in Measure-CodeQuality.ps1 or record why it is not gated
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Measure-CodeQuality.ps1, Application/Surl.Mcp, Application/Surl.Mcp.UnitTests, Documentation/Planning/Decisions, Documentation/Product/Requirements.md, CLAUDE.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2111 — Gate the Surl.Mcp executable in Measure-CodeQuality.ps1 or record why it is not gated

## Goal

The coverage of the `Surl.Mcp` executable (assembly `surl-mcp`) is either gated like `Surl.Console` and `Surl.Api`, or an ADR says why it is not.

## Context

Found while working BL-2109 (2026-11-28): `Test-IsProductionAssembly` in `Measure-CodeQuality.ps1` accepts `*.UnitLibrary`, `Surl.Console` and `Surl.Api` only, yet `Application/Surl.Mcp` holds its own code (`Program.cs`, `SurlMcpCommandLine.cs` and others). The decision is Claude's (root `CLAUDE.md`, "Decisions"). No threshold in `CodeMetricsConfig.txt` changes.

Stand-in: nothing to add; the gate changes no behaviour of a running server.

## Acceptance criteria

- [ ] Either `Measure-CodeQuality.ps1` reports `Surl.Mcp` and it passes every gate, or an ADR marked "Decided by Claude under Stewart's delegation" says why it is excluded.
- [ ] NFR-002, NFR-003 and the root `CLAUDE.md` "Quality gates" section match the outcome.
- [ ] `dotnet build` has 0 warnings and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
