---
id: BL-466
title: Serve MCP over the Streamable HTTP transport with sessions and authorization
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-465, BL-407]
touches: [Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-466 — Serve MCP over the Streamable HTTP transport with sessions and authorization

## Goal

The MCP server answers the Streamable HTTP transport on one endpoint. It answers `POST` with JSON or an SSE
stream, and `GET` as the ADR decides. It issues and checks `Mcp-Session-Id`, checks `MCP-Protocol-Version` and
`Origin`, ends a session on `DELETE`, and requires authorization by default.

## Context

- Decision: BL-463's ADR, decisions 4, 6 and 7.
- HTTP/1.1: BL-407's library (add its reference). SSE framing: the WHATWG HTML "Server-sent events"
  section (`data:`, `id:`, a blank line between events).
- MCP "Transports > Streamable HTTP":
  - `Accept` must list both types;
  - notifications and responses only get `202 Accepted`;
  - an unknown or expired session gets `404`;
  - a request without a session ID after initialization gets `400`;
  - the `MCP-Protocol-Version` header's check, and its default of `2025-03-26` when absent.
- "Authorization": `401` with `WWW-Authenticate` and RFC 9728's metadata document, as the ADR decides.
- Session IDs from an injected random source; expiry on an injected `TimeProvider`.

## Acceptance criteria

- [ ] Named tests show:
      - `initialize` by `POST` returning `Mcp-Session-Id`;
      - a later request with it answered;
      - one without it gets `400`;
      - an unknown session gets `404`;
      - a notification gets `202`;
      - a request answered on an SSE stream when the ADR says so, with SSE bytes written from the HTML standard;
      - a bad `Origin` refused with `403`;
      - an unsupported `MCP-Protocol-Version` gets `400`;
      - `DELETE` ending the session;
      - no token with anonymous off gets `401` with the ADR's `WWW-Authenticate`;
      - the metadata document served.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
