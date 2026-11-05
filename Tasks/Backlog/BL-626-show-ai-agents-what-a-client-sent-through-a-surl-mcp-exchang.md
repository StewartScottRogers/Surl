---
id: BL-626
title: Show AI agents what a client sent through a Surl.Mcp exchanges tool
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-617, BL-625]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-626 — Show AI agents what a client sent through a Surl.Mcp exchanges tool

## Goal

An agent can call one `Surl.Mcp` tool (planning name `get_exchanges`) with a server's ID and get every exchange that
server has served - listen URL, client address, start time, and the bytes received and sent in order - with bytes
shown as text where they are UTF-8 and as base64 where not, cut to the ADR's cap, so it can check exactly what its
client sent.

## Context

- Decision: BL-612's ADR, decision 3 (the tool's name, schema, result shape, the text-or-base64 rule and the cap,
  and any filter such as "since exchange N"). Servers the `start_server` tool starts turn on `Surl.Api`'s exchange
  record (BL-617, BL-611's ADR decision 7).
- The tool's logic goes in `Surl.Mcp.UnitLibrary` (BL-624); `Surl.Mcp` (BL-625) registers it with one line.
- The port's `McpReturnedBytes` (text or base64 cut to a cap, ADR-0555 of the port) is copyable code
  (`git -C Z:\repos\Curl show origin/master:Curl.Mcp.UnitLibrary/CLAUDE.md`); it is not evidence.

## Acceptance criteria

- [ ] Named tests, with the fake listener seam and a fake connection writing known bytes, show: a server's exchange
      returned with its listen URL, client address and the exact bytes received and sent; non-UTF-8 bytes returned as
      base64 and marked so; bytes past the cap cut and the cut stated as the ADR says; an unknown server ID an
      `isError` result; two exchanges returned in the order they started.
- [ ] A named test in `Surl.Mcp.UnitTests` shows `tools/list` from `RunAsync` now includes the tool, with a JSON
      object input schema and a description.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Mcp.UnitLibrary` and `Surl.Mcp` keep
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
