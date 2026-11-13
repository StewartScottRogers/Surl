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
completed: 2026-11-12
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

- [x] The test class exists with the exchanges above, each passing on Windows with the pinned client present.
- [x] `Documentation/Conformance/Extended/McpServer.md`: every row is covered by a named test or marked `n/a` with
      a reason.
- [x] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

- Class `CurlPortClientListsAndCallsToolsOnSurlOverMcpTests` (8 cases, all passing on Windows with the pinned client): list and call `echo` over `mcp` and `mcps` (`--self-signed`), list and call over `mcp+stdio` (the client starts the built `surl.exe` beside the tests, or `SURL_EXECUTABLE`; inconclusive when absent), and an unauthenticated request refused (401 with a Bearer challenge naming `resource_metadata`, checked on the wire).
- Port behaviour, not a Surl defect: for a 401 the port client exits 8 ("The server answered with status 401."), so only non-zero and the 401 text are asserted, never the code.
- McpServer.md: the rows these tests prove gain a `live:` test name; rows owned by the unit tasks keep their task IDs, which BL-464 to BL-467 and BL-619 to BL-621 resolve to unit test names themselves, so this task claims only its own rows.

## Log

- 2026-11-04: Created.
- 2026-11-06: depends-on BL-468 replaced by BL-978, BL-979 (BL-468 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Curl port mcp client lists, calls echo and is refused 401 against live surl mcp, mcps and mcp+stdio
