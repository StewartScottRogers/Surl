---
id: BL-619
title: Create Surl.McpServing with JSON-RPC 2.0 messages and the MCP lifecycle
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-612]
touches: [Protocols/Http/Surl.McpServing.UnitLibrary, Protocols/Http/Surl.McpServing.UnitTests, Surl.slnx, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-619 — Create Surl.McpServing with JSON-RPC 2.0 messages and the MCP lifecycle

## Goal

`Surl.McpServing.UnitLibrary`, the MCP core shared by the `Surl.Mcp` host and the extended MCP server, exists. It
reads and writes JSON-RPC 2.0 requests, notifications and responses, and runs the MCP lifecycle independently of any
transport: `initialize` with version negotiation and capabilities, `notifications/initialized`, `ping`, refusal of
requests before initialization, the size limit, and the error codes BL-612's ADR names. The server's name, version
and capabilities come from its caller.

## Context

- Decision: BL-612's ADR, decision 1 (name, folder, namespace, references, versions accepted, the answer to an
  unsupported version, each failure's JSON-RPC code, the message limit). If the ADR names the library or folder
  differently, use its names and record the difference in Notes.
- This takes over the transport-independent half of BL-464, which now builds the extended MCP server on this library.
- JSON-RPC 2.0 (https://www.jsonrpc.org/specification): `-32700` parse error, `-32600` invalid request, `-32601`
  method not found, `-32602` invalid params, `-32603` internal error. In MCP a request's `id` is a string or number,
  never null, and this revision has no batching (MCP "Base Protocol > Overview"). MCP "Lifecycle"
  (https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle): the `initialize` result's
  `protocolVersion`, `capabilities` and `serverInfo`, and the page's example messages.
- JSON through `Utf8JsonReader`/`Utf8JsonWriter` or a source-generated `JsonSerializerContext` only (native AOT; no
  reflection). The port's `Curl.Mcp.Protocol.UnitLibrary/JsonRpc/` and its ADR-0800 are copyable code
  (`git -C Z:\repos\Curl show origin/master:<path>`); expected values come from the specification's examples.
- A horizontal library: add its row to `ProtocolIsolationTests.HorizontalLibraries`
  (`Foundation/Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`) with the projects it may reference,
  and update that dictionary's summary to name BL-612's ADR. New projects go in `Surl.slnx`'s `/Protocols/Http/`
  folder in ordinal order.

## Acceptance criteria

- [ ] Both projects exist, are in `Surl.slnx`, have a `CLAUDE.md` in the library saying what it holds and who
      references it, and `ProtocolIsolationTests` passes with the new row.
- [ ] Named tests show: the "Lifecycle" page's `initialize` request example answered with a result in the page's
      shape carrying the caller's server info and capabilities; an unsupported `protocolVersion` answered as the
      ADR says; a request before `initialize` refused as the ADR says; `ping` answered with `{}`; a notification
      getting no response; malformed JSON getting `-32700`; a request with a null `id` getting `-32600`; an unknown
      method getting `-32601`; a message over the limit refused as the ADR says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
