# MCP server requirement matrix

These are the server-side requirements that `Surl.Extended.Protocol.McpServer` answers for the `mcp`,
`mcps` and `mcp+stdio` schemes (ADR-0083 decision 9). They are written from the Model Context Protocol
specification, revision 2025-06-18 (https://modelcontextprotocol.io/specification/2025-06-18), and the
RFCs it names, which [ADR-0111](../../Planning/Decisions/ADR-0111-how-the-mcp-server-answers-mcp-mcps-and-mcp-stdio.md)
decides by, never from the Curl port's client matrices. Each row binds the server. "Covered by" names the
task that proves the row until that task replaces its ID with the named test, or says why the row needs
no test. Rows the shared MCP core `Surl.McpServing` keeps
([ADR-0103](../../Planning/Decisions/ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md))
name the core's task (BL-619, BL-620, BL-621 or BL-623); this server's own tasks are BL-464 (lifecycle),
BL-465 (tools), BL-466 (Streamable HTTP and authorization) and BL-467 (stdio).

IDs are:

- `MCP:<Page>-<n>` for a requirement of a specification page;
- `MCP:Surl-<Decision>-<n>` for a rule ADR-0111 adds where the pages leave the server a choice.

## Base protocol ("Base Protocol > Overview")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| MCP:Base-1 | MUST | Messages are JSON-RPC 2.0 and UTF-8 encoded. | BL-619 |
| MCP:Base-2 | MUST | A response carries the same `id` as the request it answers. | BL-619 |
| MCP:Base-3 | MUST | A response holds exactly one of `result` and `error`; an error has an integer `code` and a `message`. | BL-619 |
| MCP:Base-4 | MUST NOT | A notification is not answered. | BL-619 |
| MCP:Base-5 | MUST | A request whose `id` is null or not a string or integer is refused (`-32600`, null `id`). | BL-619 |
| MCP:Base-6 | MUST | A batch (a JSON array) is refused, as this revision removed batching (`-32600`, ADR-0103). | BL-619 |
| MCP:Base-7 | MUST | `_meta` in params is accepted and ignored where the server defines no use for it. | BL-619 |
| MCP:Base-8 | MUST NOT | The server sends no request or notification of its own (ADR-0111 decision 1). | BL-464 |

## Lifecycle ("Base Protocol > Lifecycle")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| MCP:Lifecycle-1 | MUST | `initialize` is answered with `protocolVersion`, `capabilities` and `serverInfo`. | BL-464 |
| MCP:Lifecycle-2 | MUST | A supported requested version is answered with the same version. | BL-619 |
| MCP:Lifecycle-3 | MUST | An unsupported requested version is answered with another version the server supports, the latest (`2025-06-18`), never an error. | BL-464 |
| MCP:Lifecycle-4 | MUST | Only negotiated capabilities are used; the server advertises `tools` only, with `listChanged` false. | BL-464 |
| MCP:Lifecycle-5 | SHOULD NOT | The server sends no request other than pings and logging before `notifications/initialized`. | n/a: the server sends no request at all (MCP:Base-8) |
| MCP:Lifecycle-6 | SHOULD | Requests other than `ping` before `initialize` is answered are refused (`-32600`). | BL-464 |
| MCP:Lifecycle-7 | MUST | A second `initialize` in one session is refused (`-32600`). | BL-619 |
| MCP:Lifecycle-8 | MUST | `serverInfo` is `{"name": "surl", "title": "Surl MCP server", "version": ...}` and `instructions` is ADR-0111 decision 1's text. | BL-464 |
| MCP:Lifecycle-9 | SHOULD | On stdio, the server shuts down when the client closes its input. | BL-467 |
| MCP:Lifecycle-10 | SHOULD | Timeouts for sent requests. | n/a: the server sends no request (MCP:Base-8) |

## Tools ("Server Features > Tools")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| MCP:Tools-1 | MUST | A server with tools declares the `tools` capability. | BL-464 |
| MCP:Tools-2 | MUST | `tools/list` lists each tool with `name`, `description` and an `inputSchema` object. | BL-465 |
| MCP:Tools-3 | MUST | `tools/list` is paginated with an opaque `cursor`/`nextCursor`. | BL-620 |
| MCP:Tools-4 | MUST | `tools/call` with an unknown tool is a protocol error (`-32602`). | BL-620 |
| MCP:Tools-5 | MUST | Servers validate all tool inputs; arguments breaking the schema are `-32602`. | BL-620 |
| MCP:Tools-6 | MUST | A result's `content` holds text or embedded-resource items in the page's shape. | BL-465 |
| MCP:Tools-7 | MUST | A failure inside a tool is a result with `isError: true`, not a protocol error. | BL-465 |
| MCP:Tools-8 | SHOULD | `structuredContent` is also given as serialized text for older clients. | BL-465 |
| MCP:Tools-9 | MUST | Servers implement proper access controls: tool file access goes through the content store as the caller, with share access. | BL-465 |
| MCP:Tools-10 | MUST | Servers sanitize tool outputs: refusals are fixed text and never echo the path's resolution. | BL-465 |
| MCP:Tools-11 | MUST | Servers rate limit tool invocations: at most 16 requests in flight per session. | BL-621 |
| MCP:Tools-12 | SHOULD | `notifications/tools/list_changed` when the list changes. | n/a: the list never changes while serving; `listChanged` is false |
| MCP:Tools-13 | MUST | If `outputSchema` is given, structured results conform to it. | n/a: no tool sends `outputSchema` (ADR-0111 decision 2) |
| MCP:Surl-Tools-1 | MUST | `list_directory` is registered only under `--list-directories`; at most 1000 entries, flagged when truncated. | BL-465 |
| MCP:Surl-Tools-2 | MUST | `read_file` answers UTF-8 as text and other bytes as an embedded `blob`; past `--max-message` it is `isError`. | BL-465 |
| MCP:Surl-Tools-3 | MUST | `write_file` is registered only under `--allow-uploads`; exactly one of `text` and `base64`; past `--max-filesize` it is `isError` with nothing written. | BL-465 |
| MCP:Surl-Tools-4 | MUST | `echo` returns its `text` unchanged. | BL-465 |
| MCP:Surl-Tools-5 | MUST | A missing or hidden file, a refused path and a denied write each answer decision 2's fixed `isError` text. | BL-465 |

## Utilities ("Utilities > Ping", "Cancellation", "Progress", "Pagination")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| MCP:Ping-1 | MUST | `ping` is answered promptly with an empty result, before and after `initialize`. | BL-464 |
| MCP:Cancellation-1 | SHOULD NOT | No response is sent for a request cancelled by `notifications/cancelled`. | BL-621 |
| MCP:Cancellation-2 | MAY | A cancellation naming an unknown or finished request is ignored. | BL-621 |
| MCP:Cancellation-3 | MUST | On Streamable HTTP, a cancelled `tools/call` stream ends with no event. | BL-466 |
| MCP:Progress-1 | MAY | Progress notifications for a request with a `progressToken`. | n/a: the server sends no notification (MCP:Base-8) |
| MCP:Pagination-1 | MUST | Cursors are opaque; `nextCursor` is omitted on the last page. | BL-620 |
| MCP:Pagination-2 | SHOULD | An invalid cursor is `-32602`. | BL-620 |

## stdio ("Base Protocol > Transports > stdio")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| MCP:Stdio-1 | MUST | The server reads JSON-RPC messages from its standard input and writes them to its standard output, delimited by newlines. | BL-621 |
| MCP:Stdio-2 | MUST NOT | Messages contain no embedded newline. | BL-621 |
| MCP:Stdio-3 | MUST NOT | The server writes nothing to standard output that is not a valid MCP message. | BL-621 |
| MCP:Stdio-4 | MAY | The server writes UTF-8 log strings to standard error. | BL-467 |
| MCP:Stdio-5 | MUST | This server's lifecycle and tools run over the core's stream session, each response on one line. | BL-467 |
| MCP:Surl-Stdio-1 | MUST | The listen URL is `mcp+stdio:` (or `mcp+stdio://`); a host, port, path, query or fragment is `MalformedUrl` (3). | n/a: the listen URL is parsed by `Surl.Cli`, which BL-468 changes and tests |
| MCP:Surl-Stdio-2 | MUST | `mcp+stdio:` with another listen URL, or with an option writing to standard output, is `FailedInit` (2). | n/a: a command-line check BL-468 adds and tests |
| MCP:Surl-Stdio-3 | MUST | A stdio session needs no credential, and `--list-directories`, `--allow-uploads` and anonymous share access still gate the tools. | BL-467 |
| MCP:Surl-Stdio-4 | MUST | Messages over `--max-message` are the core's `-32600` with a null `id`, and the session continues. | BL-467 |

## Streamable HTTP ("Base Protocol > Transports > Streamable HTTP")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| MCP:Http-1 | MUST | One MCP endpoint path answers `POST` (and `GET`); it is the listen URL's path, `/mcp` when it has none. | BL-466 |
| MCP:Http-2 | MUST | Another path is `404` (except the RFC 9728 document). | BL-466 |
| MCP:Http-3 | MUST | Servers validate `Origin` on all incoming connections; a foreign, `null` or malformed `Origin` is `403`, an absent one accepted. | BL-466 |
| MCP:Http-4 | SHOULD | When running locally, bind only to localhost. | n/a: the listen URL names the address; the `--aihelp` example uses `127.0.0.1` (BL-468) |
| MCP:Http-5 | SHOULD | Servers implement proper authentication for all connections (decision 6). | BL-466 |
| MCP:Http-6 | MUST | A `POST` body is one JSON-RPC message; a body the core refuses is `400` with a JSON-RPC error with no `id`. | BL-466 |
| MCP:Http-7 | MUST | A `POST` of a notification or response that is accepted is `202 Accepted` with no body. | BL-466 |
| MCP:Http-8 | MUST | A `POST` of a request is answered with `Content-Type: application/json` or `text/event-stream`. | BL-466 |
| MCP:Http-9 | MUST | `tools/call` is answered as an SSE stream holding its one response; others as `application/json`. | BL-466 |
| MCP:Http-10 | SHOULD | The SSE stream eventually holds the response, then the server closes the stream. | BL-466 |
| MCP:Http-11 | MUST | SSE bytes follow the HTML standard: `data:` lines, `:` comment keep-alives every 15 s, a blank line ending an event. | BL-466 |
| MCP:Http-12 | MUST | A `POST` whose `Accept` lacks either type is `406`; a non-JSON `Content-Type` is `415`. | BL-466 |
| MCP:Http-13 | MUST | `GET` is `text/event-stream` or `405 Method Not Allowed`; this server answers `405` with `Allow: POST, DELETE`. | BL-466 |
| MCP:Http-14 | MAY | SSE event `id`s and `Last-Event-ID` resumption. | n/a: no event IDs are sent and no stream can be resumed (ADR-0111 decision 4.8) |
| MCP:Http-15 | MUST NOT | A response is not broadcast on several streams. | n/a: each response has exactly one stream, its `POST`'s |
| MCP:Http-16 | MAY | The server assigns a session ID in the `Mcp-Session-Id` header of the `initialize` answer. | BL-466 |
| MCP:Http-17 | SHOULD | The session ID is globally unique and cryptographically secure (32 random bytes, base64url). | BL-466 |
| MCP:Http-18 | MUST | The session ID holds only visible ASCII (0x21 to 0x7E). | BL-466 |
| MCP:Http-19 | SHOULD | A request without `Mcp-Session-Id` after initialization is `400 Bad Request`. | BL-466 |
| MCP:Http-20 | MUST | A request naming an ended or unknown session is `404 Not Found`. | BL-466 |
| MCP:Http-21 | MAY | The server ends a session at any time (30 minutes idle). | BL-466 |
| MCP:Http-22 | MAY | `DELETE` with `Mcp-Session-Id` ends the session (`204`). | BL-466 |
| MCP:Http-23 | MUST | `MCP-Protocol-Version` absent is taken as `2025-03-26`. | BL-466 |
| MCP:Http-24 | MUST | An invalid or unsupported `MCP-Protocol-Version` is `400 Bad Request`. | BL-466 |
| MCP:Surl-Http-1 | MUST | A session answers only the login that opened it; another login naming it gets `404`. | BL-466 |
| MCP:Surl-Http-2 | MUST | An `initialize` carrying `Mcp-Session-Id` is `400`; one past 1000 sessions is `503` with `Retry-After: 60`. | BL-466 |
| MCP:Surl-Http-3 | MUST | A body past `--max-message` is `413`, then close. | BL-466 |
| MCP:Surl-Http-4 | MUST | `mcps` is implicit TLS 1.2 or 1.3 with ALPN `http/1.1`, and needs `--cert` or `--self-signed`. | n/a: TLS wrapping is composed in `Surl.Composition`, which BL-468 changes and tests |
| MCP:Surl-Http-5 | MUST | Checks run in decision 4.2's order: head, path, `Origin`, authorization, method, headers and session, body. | BL-466 |

## Authorization ("Base Protocol > Authorization", RFC 9728, RFC 6750)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| MCP:Auth-1 | MUST | The server serves OAuth 2.0 Protected Resource Metadata (RFC 9728) at `/.well-known/oauth-protected-resource<endpoint path>`. | BL-466 |
| MCP:Auth-2 | MUST | The metadata's `authorization_servers` names at least one authorization server (the server's own origin). | BL-466 |
| MCP:Auth-3 | MUST | A `401` carries `WWW-Authenticate: Bearer` with `resource_metadata` naming the metadata URL. | BL-466 |
| MCP:Auth-4 | MUST | Access tokens are read only from the `Authorization: Bearer` header, never from the query string. | BL-466 |
| MCP:Auth-5 | MUST | A missing or invalid token is `401`; an invalid one adds `error="invalid_token"` (RFC 6750 section 3.1). | BL-466 |
| MCP:Auth-6 | MUST | Tokens are validated as issued for this server: configured tokens by account, issued tokens by their `resource` audience. | BL-466 |
| MCP:Auth-7 | MUST | A malformed authorization request (another `Authorization` scheme) is `400`. | BL-466 |
| MCP:Auth-8 | MUST | Insufficient scope is `403`. | n/a: accounts carry no scopes; gates answer inside the tools (ADR-0111 decision 6.1) |
| MCP:Auth-9 | SHOULD NOT | Implementations using stdio do not follow the HTTP authorization flow. | BL-467 |
| MCP:Auth-10 | SHOULD | The authorization server supports RFC 8414 metadata, RFC 7591 registration, PKCE and the RFC 8707 `resource` parameter. | n/a: the authorization server is BL-720's, which adds its own rows |
| MCP:Surl-Auth-1 | MUST | No token is accepted as anonymous only under `--allow-anonymous`; a bad token is never served as anonymous. | BL-466 |
| MCP:Surl-Auth-2 | MUST | A bearer token over plain `mcp` is refused as invalid unless `--allow-plaintext-auth`. | BL-466 |
| MCP:Surl-Auth-3 | MUST | Each refusal waits the failed-login delay. | BL-466 |

## Errors and limits (ADR-0111 decision 7; ADR-0006)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| MCP:Errors-1 | MUST | JSON-RPC failures carry ADR-0103 decision 1's codes and messages; `-32603` never carries exception text. | BL-619 |
| MCP:Errors-2 | MUST | `-32002` is never sent, as no resources are served. | BL-464 |
| MCP:Surl-Limits-1 | MUST | `--max-message` bounds every JSON-RPC message on both transports, in place of the core's 4 MiB default. | BL-466 |
| MCP:Surl-Limits-2 | MUST | At most 16 requests are in flight per HTTP session; a 17th waits. | BL-466 |
| MCP:Surl-Limits-3 | MUST | Past `--max-connections` or `--max-connections-per-address` the answer is `503`, no body, then close. | BL-466 |
