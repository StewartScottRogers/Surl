---
id: BL-464
title: Create the MCP server over Surl.McpServing's JSON-RPC messages and lifecycle
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-463, BL-404, BL-619]
touches: [Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
---
# BL-464 — Create the MCP server over Surl.McpServing's JSON-RPC messages and lifecycle

## Goal

`Surl.Extended.Protocol.McpServer.UnitLibrary` exists and runs the MCP lifecycle through the shared core
`Surl.McpServing` (BL-619): it gives the core this server's `serverInfo` and capabilities, so `initialize` with
version negotiation, `notifications/initialized`, `ping`, refusal of requests before initialization and the error
codes are the core's, answered independently of any transport.

## Context

- Decision: BL-463's ADR, decisions 1, 3 and 7, which adopt BL-612's ADR for the core's versions and error codes.
  Rules: BL-543's ADR.
- Amended 2026-11-04: JSON-RPC 2.0 parsing and writing and the lifecycle are no longer written here; they live in
  `Protocols/Http/Surl.McpServing.UnitLibrary` (BL-619), shared with the `Surl.Mcp` host. This server references it
  (its row is in `ProtocolIsolationTests.HorizontalLibraries`) and adds only what is its own.
- New projects in `Protocols/Http/`, listed in `Surl.slnx`. Namespace `Surl.Extended.Protocol.McpServer`.
  References: `Surl.Protocol.Abstractions` and `Surl.McpServing`.
- MCP "Lifecycle" (https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle): the `initialize`
  result's `protocolVersion`, `capabilities` and `serverInfo`, and the example messages on that page.

## Acceptance criteria

- [ ] Both projects exist, are in `Surl.slnx`, reference `Surl.McpServing`, and `ProtocolIsolationTests` passes.
- [ ] Named tests show, through this server's session:
      - the "Lifecycle" page's `initialize` request example answered with a result in the page's shape carrying this
        server's `serverInfo` as BL-463's ADR names it;
      - an unsupported `protocolVersion` answered as BL-612's ADR says;
      - a request before `initialize` refused;
      - `ping` answered with `{}`.
- [ ] No JSON-RPC parsing or writing code exists in this library (it is the core's).
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended by task-planner: builds on the shared MCP core `Surl.McpServing` (BL-619, decided by BL-612)
  instead of writing JSON-RPC and the lifecycle itself; the core's own tests now carry the malformed-JSON, null-id,
  unknown-method, notification and size-limit cases. Title changed to match.
