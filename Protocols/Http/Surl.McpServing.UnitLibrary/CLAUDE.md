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
- The MCP lifecycle: `McpRequestDispatcher` answers one session's messages one at a time -
  `initialize` with version negotiation (`McpProtocolVersions`: `2025-06-18`, `2025-03-26`,
  `2024-11-05`; any other is answered `2025-06-18`), `ping`, `notifications/initialized`, refusal of
  requests before `initialize`, a second `initialize`, unknown methods, and the message limit.
  `McpSessionOptions` carries the caller's `McpServerInfo`, `McpServerCapabilities`, instructions and
  limit (4 MiB by default).

Planned beside it: the tool registry and pagination (BL-620), `McpStreamSession` over a stream pair
(BL-621), and resources (BL-623).

It **references nothing** - its row in `ProtocolIsolationTests.HorizontalLibraries` is empty. It takes
bytes and `JsonElement`s, constructs no socket and never touches `Console`. JSON goes through
`System.Text.Json`'s `JsonDocument` and `Utf8JsonWriter` only: no reflection, native AOT safe.

Its tests replay the MCP specification's own examples (revision 2025-06-18, "Base Protocol > Lifecycle")
and the JSON-RPC 2.0 specification's error codes, never the Curl port (ADR-0003).
