---
id: BL-464
title: Create the MCP server with JSON-RPC messages and the lifecycle
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-463, BL-404]
touches: [Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
---
# BL-464 — Create the MCP server with JSON-RPC messages and the lifecycle

## Goal

`Surl.Extended.Protocol.McpServer.UnitLibrary` exists. It reads and writes JSON-RPC 2.0 requests, notifications
and responses, and runs the MCP lifecycle independently of any transport: `initialize` with version negotiation
and capabilities, `notifications/initialized`, `ping`, refusal of requests before initialization, and the error
codes the ADR names.

## Context

- Decision: BL-463's ADR, decisions 1, 3 and 7. Rules: BL-543's ADR.
- New projects in `Protocols/Http/`, listed in `Surl.slnx`. Namespace `Surl.Extended.Protocol.McpServer`.
  References: `Surl.Protocol.Abstractions` only for now.
- JSON-RPC 2.0 (https://www.jsonrpc.org/specification): `-32700` parse error, `-32600` invalid request, `-32601`
  method not found, `-32602` invalid params, `-32603` internal error. A request's `id` is a string or number and
  never null in MCP. MCP "Lifecycle": the `initialize` result's `protocolVersion`, `capabilities` and
  `serverInfo`, and the example messages on that page.
- JSON through `Utf8JsonReader`/`Utf8JsonWriter` or a source-generated context (native AOT).

## Acceptance criteria

- [ ] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [ ] Named tests show:
      - the "Lifecycle" page's `initialize` request example answered with a result in the page's shape;
      - an unsupported `protocolVersion` answered as the ADR says;
      - a request before `initialize` refused;
      - `ping` answered with `{}`;
      - a notification getting no response;
      - malformed JSON gets `-32700`;
      - a request with a null `id` gets `-32600`;
      - an unknown method gets `-32601`;
      - a message over the limit refused.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
