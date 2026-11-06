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
completed: 2026-11-05
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

- [x] Both projects exist, are in `Surl.slnx`, have a `CLAUDE.md` in the library saying what it holds and who
      references it, and `ProtocolIsolationTests` passes with the new row.
- [x] Named tests show: the "Lifecycle" page's `initialize` request example answered with a result in the page's
      shape carrying the caller's server info and capabilities; an unsupported `protocolVersion` answered as the
      ADR says; a request before `initialize` refused as the ADR says; `ping` answered with `{}`; a notification
      getting no response; malformed JSON getting `-32700`; a request with a null `id` getting `-32600`; an unknown
      method getting `-32601`; a message over the limit refused as the ADR says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- Names as ADR-0103 decision 1 gives them: `Protocols/Http/Surl.McpServing.UnitLibrary`, namespace
  `Surl.McpServing`, referencing nothing (row `[McpServing] = []`). No difference to record.
- Shape: `JsonRpcMessageParser` (ADR-0800 read rules, copied from the port's code and adjusted) and
  `JsonRpcResponseWriter`; `McpRequestDispatcher.Answer(bytes)` answers one message per call,
  transport-independent, so BL-621's `McpStreamSession` and the extended server's Streamable HTTP feed it
  one message at a time. `AnswerMessageTooLong()` is there for BL-621, which discards an over-long line
  without buffering it. `NegotiatedProtocolVersion` is there for BL-620 (`structuredContent` only on
  `2025-06-18`).
- Default taken: the dispatcher is synchronous (`byte[]? Answer`), since nothing in the lifecycle awaits;
  BL-620 makes it async when tool handlers arrive.
- Default taken: capabilities come from the caller as `McpServerCapabilities(Tools, Resources)`; BL-620's
  registry will supply them from what is registered.
- Departure from ADR-0103's wording "one source-generated `JsonSerializerContext` and `Utf8JsonWriter`":
  responses are written with `Utf8JsonWriter` alone. The three shapes are fixed, the writer is AOT-safe
  with no generated code to exclude from coverage, and the bytes are the same. BL-620 may add a context if
  a result type warrants one.
- `title` in `serverInfo` is written only when `2025-06-18` is negotiated (the ADR's rule for `title`), so
  the Lifecycle example, which asks for `2024-11-05`, is answered without it;
  `Answer_InitializeLatestWithNoCapabilitiesOrInstructions_WritesTheTitleAndEmptyCapabilities` shows it with.
- Tests: `McpRequestDispatcherTests` covers every acceptance case by name (`Answer_LifecycleInitializeExample_…`,
  `Answer_InitializeWithAnUnsupportedProtocolVersion_…`, `Answer_RequestBeforeInitialize_…`,
  `Answer_PingBeforeAndAfterInitialize_…`, `Answer_NotificationOrClientResponse_GetsNoResponse`,
  `Answer_MalformedJson_…`, `Answer_RequestWithANullId_…`, `Answer_RequestsAfterInitialize…_AreAccepted`
  (-32601), `Answer_MessageOverTheLimit_…`). 62 tests; library at 100% line and branch coverage.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.McpServing reads and writes JSON-RPC 2.0 and answers the MCP lifecycle: initialize with version negotiation, ping, notifications, refusals and the 4 MiB limit
