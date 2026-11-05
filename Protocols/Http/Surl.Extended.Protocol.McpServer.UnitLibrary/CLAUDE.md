# Surl.Extended.Protocol.McpServer.UnitLibrary

An extended server (ADR-0083): it will answer the `mcp`, `mcps` and `mcp+stdio` schemes the Curl
port adds, which upstream curl does not have. ADR-0111 decides how. It is judged by the MCP
specification (revision 2025-06-18 and the two before it), never by the Curl port's output, and its
unit tests replay messages written by hand from that specification's examples.

It references `Surl.Protocol.Abstractions` and the shared MCP core `Surl.McpServing` (ADR-0103).
JSON-RPC 2.0 parsing and writing, the lifecycle, version negotiation and the error codes are the
core's; never write them here.

What is here so far is the lifecycle (BL-464, ADR-0111 decisions 1, 3 and 7):

- `McpServerIdentity` is this server's `serverInfo` (`surl`, `Surl MCP server`, surl's version
  given by the host) and its `instructions`.
- `McpServerSession` is one transport-independent session: it gives the core that identity and the
  server's registry, and the core answers `initialize`, `notifications/initialized`, `ping`, refuses
  requests before `initialize`, and writes the JSON-RPC errors. `CreateOptions` makes the options
  every session of the server shares, with `--max-message` as the message limit.

Still to come: the tools (BL-465), Streamable HTTP (BL-466), the stdio session (BL-467) and the
command line (BL-468).
