# Surl.McpServing.UnitLibrary

A horizontal library: the transport-independent MCP core
([ADR-0103](../../../Documentation/Planning/Decisions/ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md)
decision 1), shared by the `Surl.Mcp` host (`Application/Surl.Mcp.UnitLibrary`, planned) and the extended
MCP server `Surl.Extended.Protocol.McpServer` (planned), so the two never drift.

What it holds, all in namespace `Surl.McpServing`, every type `public`:

- JSON-RPC 2.0 messages: `JsonRpcMessageParser` reads one message into a `JsonRpcRequest`,
  `JsonRpcNotification`, `JsonRpcClientResponse` (ignored) or `JsonRpcInvalidMessage`;
  `JsonRpcResponseWriter` writes a response as one line of UTF-8 JSON with `Utf8JsonWriter`;
  `JsonRpcRequestId` echoes an id byte for byte; `JsonRpcError` and `JsonRpcErrorCodes` are ADR-0103's
  error table.
- The MCP lifecycle: `McpRequestDispatcher.AnswerAsync` answers one session's messages, one message per
  call - `initialize` with version negotiation (`McpProtocolVersions`: `2025-06-18`, `2025-03-26`,
  `2024-11-05`; any other is answered `2025-06-18`), `ping`, `notifications/initialized`, refusal of
  requests before `initialize`, a second `initialize`, unknown methods, and the message limit.
  `McpSessionOptions` carries the caller's `McpServerInfo`, instructions, message limit (4 MiB by
  default) and list page size (100 by default).
- Tools: `McpServerRegistry.AddTool` adds one `McpTool` (name, title, description, hand-written input
  schema JSON, async handler) per call, refusing a duplicate name or a schema `McpToolInputSchema` cannot
  check (it supports exactly ADR-0103's JSON Schema subset). The `initialize` result advertises `tools`
  only once a tool is added. `McpToolMethods` answers `tools/list` paged by `McpListCursor` (the next
  offset as decimal text, base64url without padding; a bad cursor is `-32602 Invalid params: cursor`) and
  `tools/call`: arguments are checked before the handler runs (`-32602` naming the first failing path),
  the handler's `McpToolResult` is written as `content`/`structuredContent`/`isError` - its text blocks,
  then its `McpEmbeddedResource` blocks (`{"type":"resource","resource":{"uri","mimeType","blob"}}`, the
  bytes base64) - an
  `McpToolFailureException` becomes `isError: true`, any other exception `-32603 Internal error`, and a
  handler cancelled through its token gets no response.

- The stdio session: `McpStreamSession.RunAsync(input, output, registry, options, token)` serves one
  session over a stream pair. `McpInputLineReader` reads newline-delimited messages (a trailing `\r` is
  dropped, an empty line skipped, an unterminated last line answered, a line over the limit read to its
  newline and discarded, then answered `-32600` with a null `id`). Each response is written as one line
  ending in `\n`, a whole line per write behind one lock, flushed. Up to `MaximumConcurrentRequests` (16)
  requests run at once; a slot is taken before each message is read, so the 17th waits unread.
  `notifications/cancelled` cancels the named request's token (a string id and a number id never match);
  a cancelled request's response is never written. The session returns when the input ends or the token
  is cancelled, after cancelling the running handlers and waiting for them; a failed read or a failed
  write of a running answer is thrown once they have ended.

Planned beside it: resources (BL-623), which adds the `resources` capability to the registry.

It **references nothing** - its row in `ProtocolIsolationTests.HorizontalLibraries` is empty. It takes
bytes and `JsonElement`s, constructs no socket and never touches `Console`. JSON goes through
`System.Text.Json`'s `JsonDocument` and `Utf8JsonWriter` only: no reflection, native AOT safe.

Its tests replay the MCP specification's own examples (revision 2025-06-18, "Base Protocol > Lifecycle")
and the JSON-RPC 2.0 specification's error codes, never the Curl port (ADR-0003).
