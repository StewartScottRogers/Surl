---
id: BL-469
title: Run the Curl port's mcp client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-978, BL-979, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/McpServer.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-469 — Run the Curl port's mcp client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl mcp://` and `mcps://`, and against
`surl` started as an `mcp+stdio` server program. Each lists tools, calls one, and has an unauthenticated HTTP
request refused. Each assertion comes from MCP 2025-06-18, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. For stdio, the port's client starts
  the published or built `surl` as its server program. Command lines: the port's `curl --extended-help mcp` and
  its cases in `Curl.Conformance.Extended.UnitTests/McpClient/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [ ] The test class exists with the exchanges above, each passing on Windows with the pinned client present.
- [ ] `Documentation/Conformance/Extended/McpServer.md`: every row is covered by a named test or marked `n/a` with
      a reason.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-06: depends-on BL-468 replaced by BL-978, BL-979 (BL-468 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-12: Backlog -> Doing.
