# ADR-0111 — How the MCP server answers `mcp`, `mcps` and `mcp+stdio`

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-463. BL-464 creates the server and its lifecycle (decisions 1 and 3). BL-465 adds its tools
  (decision 2). BL-466 serves Streamable HTTP with sessions, the `Origin` check, authorization and the
  limits (decisions 4, 6 and 7). BL-467 serves the stdio session (decision 5's library half). BL-468
  serves all three schemes from `surl` (decision 5's command-line half and decision 8). BL-722 serves
  the OAuth 2.1 authorization server (decision 6.3).
- **Builds on:** [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  (the oracle is the specification; the Curl port is never proof; default ports; `--aihelp` shape;
  the matrix), [ADR-0084](ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md)
  decisions 1, 4 and 5 (`Surl.HttpServing`, logins through `IHttpAuthenticationSession`, files on
  `Surl.Content`), [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decisions 2 and 4.1 (the shared content store and accounts), [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)
  decision 2.3 (share access), [ADR-0103](ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md)
  (the MCP core `Surl.McpServing`, whose decision 1 binds this ADR's decisions 3 and 7),
  [ADR-0110](ADR-0110-how-the-google-cloud-storage-server-answers-gs.md) decision 3.2 (opaque Bearer
  tokens against empty-name accounts), [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)
  (secure by default) and [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) (limits, fixed
  error text).

## Context

`Surl.Extended.Protocol.McpServer` (in `Protocols/Http/`, ADR-0083's table) answers the `mcp`, `mcps`
and `mcp+stdio` schemes the Curl port adds. By ADR-0083 decision 1 it is judged by the Model Context
Protocol specification, revision 2025-06-18, https://modelcontextprotocol.io/specification/2025-06-18,
not by upstream curl, which has no MCP scheme, and not by the Curl port. The pages read on 2026-11-05
are cited below by title:

- **"Base Protocol > Overview"** (`basic`): JSON-RPC 2.0; request `id` a string or integer, never null,
  unique per session; responses carry `result` or `error`; notifications are not answered; this
  revision has no batching; `_meta` is reserved.
- **"Base Protocol > Lifecycle"** (`basic/lifecycle`): `initialize`, version negotiation, capability
  negotiation, `notifications/initialized`, "The server SHOULD NOT send requests other than pings and
  logging before receiving the `initialized` notification", shutdown by closing stdio input or the HTTP
  connections, and timeouts.
- **"Base Protocol > Transports"** (`basic/transports`): stdio ("The server MUST NOT write anything to
  its `stdout` that is not a valid MCP message", "MAY write UTF-8 strings to its standard error") and
  Streamable HTTP: one MCP endpoint answering `POST` and `GET`; "Servers MUST validate the `Origin`
  header on all incoming connections to prevent DNS rebinding attacks", "When running locally, servers
  SHOULD bind only to localhost", "Servers SHOULD implement proper authentication"; a notification or
  response `POST` answered `202 Accepted` with no body, or an HTTP error status with an optional
  JSON-RPC error with no `id`; a request `POST` answered with `Content-Type: application/json` or
  `text/event-stream`; `GET` answered with `text/event-stream` or `405 Method Not Allowed`; SSE event
  `id`s and `Last-Event-ID` resumption (both MAY); `Mcp-Session-Id` (visible ASCII 0x21 to 0x7E,
  "SHOULD be globally unique and cryptographically secure"), `400 Bad Request` for a request without it
  after initialization, `404 Not Found` once a session is ended, `DELETE` to end one (`405` if the
  server refuses); the `MCP-Protocol-Version` header, assumed `2025-03-26` when absent, and "If the
  server receives a request with an invalid or unsupported `MCP-Protocol-Version`, it MUST respond with
  `400 Bad Request`".
- **"Base Protocol > Authorization"** (`basic/authorization`): optional; for HTTP transports the
  server is an OAuth 2.1 resource server; "MCP servers MUST implement the OAuth 2.0 Protected Resource
  Metadata (RFC 9728)"; the metadata's `authorization_servers` names at least one authorization server;
  "MCP servers MUST use the HTTP header `WWW-Authenticate` when returning a 401 Unauthorized to indicate
  the location of the resource server metadata URL"; tokens only in the `Authorization: Bearer` header,
  never in the query string; `401` for a missing or invalid token, `403` for insufficient scope, `400`
  for a malformed request; servers "MUST validate that access tokens were issued specifically for them"
  (RFC 8707 audience); "Implementations using an STDIO transport SHOULD NOT follow this specification,
  and instead retrieve credentials from the environment".
- **"Server Features > Tools"** (`server/tools`): the `tools` capability with `listChanged`;
  `tools/list` with pagination; a tool's `name`, `title`, `description`, `inputSchema`, optional
  `outputSchema` and `annotations`; `tools/call` with `name` and `arguments`; result `content` (text,
  image, audio, resource link, embedded resource), `structuredContent`, `isError`; "Servers MUST validate
  all tool inputs", "implement proper access controls", "rate limit tool invocations", "sanitize tool
  outputs"; unknown tools and invalid arguments are protocol errors, failures inside a tool are
  `isError` results.
- **"Utilities"**: "Ping" (answer promptly with an empty result), "Cancellation"
  (`notifications/cancelled`; the receiver "SHOULD NOT send a response for the cancelled request"),
  "Progress" (optional) and "Pagination" (opaque `cursor`/`nextCursor`, an invalid cursor `-32602`).
- **RFC 9728** (protected resource metadata, the `/.well-known/oauth-protected-resource` URI with the
  resource's path appended, the `resource_metadata` parameter of `WWW-Authenticate`), **RFC 6750**
  (Bearer, `error="invalid_token"`), and the **WHATWG HTML "Server-sent events"** section (`data:`,
  `id:`, comment lines, a blank line ending an event).

What a client sends was read (for what to accept, never as a verdict) from the port's
`Curl.Extended.Protocol.McpClient.UnitLibrary` and its matrices `McpClient.md` and
`McpClient-Stdio.md`: one `POST` per message to the URL's path with `Accept: application/json,
text/event-stream`, `Content-Type: application/json`, `Mcp-Session-Id` and `MCP-Protocol-Version` once
known, a `DELETE` at the end, a fresh `initialize` after a `404`; no `Authorization` and no `Origin`;
and, for stdio, `mcp+stdio://localhost/<command>?<arguments>`, which starts the named program and
speaks to its standard input and output.

ADR-0103 has already decided the MCP core both this server and `surl-mcp` use: the JSON-RPC messages
and error table, the lifecycle with the versions `2025-06-18`, `2025-03-26` and `2024-11-05`, the tool
registry with its schema subset, pagination at 100, and `McpStreamSession`. This ADR decides only what
is this server's own.

## Decision

### 1. The project

`Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary`, namespace
`Surl.Extended.Protocol.McpServer`, tested by `Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests`.
The name is confirmed: it is the server side of the port's McpClient and says what it is. It references
`Surl.Protocol.Abstractions`, `Surl.McpServing` (ADR-0103), `Surl.HttpServing` (ADR-0084 decision 1;
Streamable HTTP) and `Surl.Content` (the tools' files), all on ADR-0002 decision 3's table as amended.
It references no original server and no other extended server.

- **One registry per server.** The server builds one `McpServerRegistry` at start (decision 2's tools,
  no resource provider), with `serverInfo` `{"name": "surl", "title": "Surl MCP server", "version":
  <surl's informational version>}` and the `instructions` "This server lists, reads and, when uploads
  are allowed, writes the files Surl serves. Paths are '/'-separated and relative to the served
  directory." Its capabilities are therefore `{"tools": {"listChanged": false}}` only (ADR-0103
  decision 1): no `resources`, `prompts`, `logging` or `completions`.
- **One dispatcher, two transports.** Streamable HTTP (decision 4) feeds the core's request dispatcher
  one message at a time per session; stdio (decision 5) runs the core's `McpStreamSession`. The
  lifecycle state, version and tool calls are the core's in both.
- **The server sends no request and no notification of its own**: no sampling, elicitation, roots,
  logging, progress or `list_changed`. Every message it writes is a response to a client request. This
  is what lets decision 4 answer `GET` with `405` and need no resumption.

### 2. The tools

Four tools, in this `tools/list` order, each added with one `AddTool` line and a hand-written schema in
ADR-0103's subset. Names and property names are snake_case. Each result carries one text block and,
where stated, the same facts as `structuredContent`; `outputSchema` is not sent (it is optional, and
the core writes `structuredContent` only when `2025-06-18` was negotiated).

**`list_directory`** - "List the files and folders in a folder Surl serves." Schema: `path` (string,
optional, default `""`, the served root). Result: one text line per entry, `<name>` or `<name>/` for a
folder, in ordinal order, and `structuredContent` `{"entries": [{"name", "kind": "file"|"directory",
"size"}]}` (`size` only for files). At most 1000 entries; past that `structuredContent` adds
`"is_truncated": true` and the text ends with the line `...`. **Registered only under
`--list-directories`** (ADR-0006 section 2: listings are off by default), so a default server does not
offer it.

**`read_file`** - "Read one file Surl serves." Schema: `path` (string, required, `minLength` 1).
Result: a well-formed UTF-8 file is one `text` block holding its text; any other file is one `resource`
block (an embedded resource) with `uri` `file:///<path>`, `mimeType` `application/octet-stream` and the
bytes as `blob` (base64). A file whose answer would pass the message limit (decision 7) is `isError`
`the file is larger than this server returns in one message`.

**`write_file`** - "Create or replace one file Surl serves." Schema: `path` (string, required,
`minLength` 1), `text` (string) and `base64` (string), `additionalProperties: false`; exactly one of
`text` and `base64` is checked by the tool (the subset has no `oneOf`), and both or neither is
`isError` `give exactly one of text and base64`. Result: the text `wrote <n> bytes to <path>` and
`structuredContent` `{"path", "size"}`. **Registered only under `--allow-uploads`** (ADR-0006 section
2), so without it a call is the core's `-32602 Invalid params: unknown tool write_file`. A file past
`--max-filesize` is `isError` before anything is written.

**`echo`** - "Return the text it is given, unchanged." Schema: `text` (string, required). Result: one
text block holding `text`. It is the tool a client checks a session with, and touches nothing.

Paths are `/`-separated and relative to the content store's root (ADR-0100 decision 2.1); a leading
`/` is ignored. Every file operation goes through `Surl.Content` as the caller - the authenticated
account, or anonymous under `--allow-anonymous` - so the store's own rules apply unchanged: `..`,
dot-files without `--serve-dot-files`, `.surl` and paths outside the root are refused, and share access
(ADR-0102 decision 2.3) applies. Each refusal is an `isError` result with fixed text that never echoes
the path's resolution:

| Failure | `isError` text |
| --- | --- |
| No such file or folder, or one the caller's share access hides | `no such file or folder` |
| A path the content store refuses (outside the root, `..`, a dot-file, `.surl`) | `the path is refused` |
| A write the caller's share access denies | `writing is not permitted` |
| A file past `--max-filesize` on write | `the file is larger than --max-filesize` |
| A read past the message limit | `the file is larger than this server returns in one message` |

A hidden file and a missing one answer alike, as ADR-0102 decision 2.3 answers a denied read. Argument
errors (a missing `path`, a wrong type) are the core's `-32602`, as the Tools page separates them.

### 3. Version negotiation

Adopted from ADR-0103 decision 1, not decided again: `2025-06-18`, `2025-03-26` and `2024-11-05` are
answered with the same version; any other string is answered with `2025-06-18`, never with an error
("Lifecycle", "Version Negotiation"). On Streamable HTTP the negotiated version also decides the
`MCP-Protocol-Version` check of decision 4.6.

### 4. Streamable HTTP ("Transports > Streamable HTTP")

#### 4.1 The endpoint

The MCP endpoint is the listen URL's path. When the listen URL has no path or only `/`, it is
**`/mcp`**, the path the specification's and the port's examples use. A request to any other path is
`404 Not Found` with no body, except the RFC 9728 metadata document of decision 6.2. A query string on
the endpoint is ignored.

#### 4.2 The order of checks

The request head and its limits (`Surl.HttpServing`), then the path, then `Origin` (4.7), then
authorization (decision 6), then the method, then the headers and session (4.3 to 4.6), then the body.
A peer that fails an earlier check learns nothing of the later ones.

#### 4.3 `POST`

- **Headers.** `Accept` must list both `application/json` and `text/event-stream` (media ranges `*/*`
  and `application/*`, `text/*` count; `q=0` excludes), else **`406 Not Acceptable`**. `Content-Type`
  must be `application/json` (parameters allowed), else **`415 Unsupported Media Type`**.
- **The body is one JSON-RPC message**, read by the core. A body the core refuses (`-32700`, `-32600`,
  a batch) is **`400 Bad Request`** with `Content-Type: application/json` and the core's error with a
  null `id`, as the page allows.
- **A notification or a response** is **`202 Accepted`** with no body. `notifications/cancelled`
  cancels the named request in this session (ADR-0103 decision 1).
- **A request** is answered with **`200 OK`** and either:
  - **`application/json`** - one JSON-RPC response - for every method except `tools/call`; or
  - **`text/event-stream`** for **`tools/call`**: the response is one SSE event, `event: message` is
    not sent (the default type), `data: <the response on one line>`, then a blank line, and the stream
    ends (the connection stays persistent where HTTP/1.1 allows; the body is chunked). While the tool
    runs, a comment line `: keep-alive` and a blank line are written every 15 seconds on the injected
    `TimeProvider`, so an idle-timeout on either side does not cut a long call. No `id:` field is sent
    (decision 4.8). A cancelled call ends the stream with no event.
  The rule is fixed by method, not by `Accept`, because both types are always acceptable (above).

#### 4.4 `GET`, `DELETE` and other methods

- **`GET`** on the endpoint is **`405 Method Not Allowed`** with `Allow: POST, DELETE`: the server
  sends nothing unsolicited (decision 1), so a server-to-client stream would stay empty.
- **`DELETE`** with a known `Mcp-Session-Id` ends the session (its running tool calls are cancelled)
  and is **`204 No Content`**; without the header it is `400`, with an unknown one `404`.
- **Any other method** is `405` with the same `Allow`.

#### 4.5 Sessions

- **Assigned at `initialize`.** The `200` answering a successful `initialize` carries
  `Mcp-Session-Id: <id>`: 32 bytes from an injected random source (the production one
  `RandomNumberGenerator`), base64url without padding - 43 characters, all visible ASCII. An
  `initialize` that carries an `Mcp-Session-Id` is `400`: a client starts a new session without one, as
  the page's "Session Management" says.
- **Required after.** Any other `POST` without `Mcp-Session-Id` is **`400 Bad Request`**; one naming
  no live session is **`404 Not Found`**, no body, and the client starts again.
- **Bound to the login.** A session belongs to the account (or anonymous caller) that opened it; a
  request with another login naming it is `404`, as if it did not exist, so a session ID is not a
  credential on its own.
- **Expiry.** A session ends after **30 minutes without a request**, on the injected `TimeProvider`,
  or on `DELETE`; its ID is then unknown (`404`). IDs are never reused.
- **Bound: 1000 sessions** a server. An `initialize` past it is **`503 Service Unavailable`** with
  `Retry-After: 60` and no body, and no session is made.

#### 4.6 `MCP-Protocol-Version`

On every request but `initialize`: absent is taken as `2025-03-26` (the page's "SHOULD assume"); a
value that is not one of decision 3's three versions is **`400 Bad Request`** with no body. A supported
version other than the negotiated one is accepted: the page requires refusing only an invalid or
unsupported one, and the core writes the negotiated version's shapes regardless.

#### 4.7 `Origin`

The page requires validating `Origin` on every request. **A request without `Origin` is accepted**:
non-browser clients (the port's client, an SDK, a test) send none, and DNS rebinding needs a browser,
which always sends it on a cross-origin `POST`. **A request with `Origin` is accepted only when** its
value parses as `<scheme>://<host>[:<port>]` and either its host is loopback (`localhost`, an IPv4
address in `127.0.0.0/8`, `[::1]`) or its scheme, host and port equal the request's own (`http` for
`mcp`, `https` for `mcps`, and the `Host` header). Anything else - another site, `null`, a value that
does not parse - is **`403 Forbidden`** with no body. No option widens it: a browser-based client is
served from the same origin or from loopback.

#### 4.8 SSE event IDs and resumption

No `id:` field is written and `Last-Event-ID` is not honoured: the only streams are single-response
`tools/call` streams (4.3), and `GET` is `405` (4.4), so there is nothing to resume on. A client whose
stream breaks sends the call again. Both are MAY in the page.

#### 4.9 `mcps`

`mcps` is the same endpoint over implicit TLS from the first byte, wrapped in `ImplicitTlsSchemeServer`
as `https` is (ADR-0010), on port 443 (ADR-0083 decision 7), TLS 1.2 and 1.3 (ADR-0006 section 4),
ALPN `http/1.1` only. `--cert` or `--self-signed` is required, else the start fails with
`CertificateProblem` (58), as for every secure listen URL (ADR-0032). `mcp` is plain HTTP/1.1 on port
80, as ADR-0083 decision 7 fixed; no `--mcp-plaintext` option is needed, since the scheme itself is the
plain one.

### 5. stdio: `mcp+stdio`

- **The listen URL** is **`mcp+stdio:`** (`mcp+stdio://` with an empty authority is the same). It
  names no host, port, path, query or fragment; any of those is refused `MalformedUrl` (3) naming the
  part. It means: serve one MCP session on `surl`'s own standard input and output. A client starts it as
  a program, e.g. the port's `mcp+stdio://localhost/surl?mcp%2Bstdio%3A` runs `surl mcp+stdio:`.
- **It is the only listen URL.** `mcp+stdio:` with any other listen URL is refused `FailedInit` (2)
  with `mcp+stdio: serves standard input and output and cannot be combined with another listen URL`.
  The session's lifetime is the process's ("Lifecycle > Shutdown": the client ends a stdio server by
  closing its input), so another listener would either die with it unannounced or keep a process alive
  that its client believes ended.
- **Standard output carries only MCP messages.** Every log line - start-up warnings, status lines and
  the `-v` exchange log - goes to standard error ("Transports > stdio"). An option that would write
  anything else to standard output (`--trace -`, `--trace-ascii -`, `--log-file -`) is refused
  `FailedInit` (2) before serving. The "listening on" status line names `mcp+stdio:` on standard error.
- **Lifetime.** `surl` serves the session through the core's `McpStreamSession` (ADR-0103 decision 1)
  bound to its standard streams through an injected seam, so tests use in-memory streams. When its
  input ends, it cancels any running tool call, waits for it, and exits `0`; Ctrl+C or SIGTERM does the
  same. It never closes standard output early.
- **Authorization.** None: the client that started the process is trusted, as the Authorization page
  says stdio implementations retrieve credentials from the environment. The tools run as the anonymous
  caller with the same option gates (`--list-directories`, `--allow-uploads`, share access for
  anonymous), and `--allow-anonymous` is neither needed nor warned for `mcp+stdio:`. This is a deliberate
  secure-by-default reading: whoever can start `surl` with chosen options already has every right those
  options grant.
- `SURL_EXTENSIONS=0` makes `mcp+stdio:` `UnsupportedProtocol` (1), as every extended scheme.

### 6. Authorization (HTTP transports)

#### 6.1 What is required

- **By default every `mcp` and `mcps` request to the endpoint carries `Authorization: Bearer <token>`**
  naming an account. Tokens are opaque and checked by `Surl.Authentication`'s `BearerAuthenticationMethod`
  against the empty-name accounts' passwords (`--user ':<token>'`, `--user-file`) in constant time,
  through `IHttpAuthenticationSession` (ADR-0084 decision 4; ADR-0110 decision 3.2), and against the
  tokens decision 6.3's authorization server issued for this resource.
- **No token, anonymous off:** **`401 Unauthorized`** with `WWW-Authenticate: Bearer
  resource_metadata="<metadata URL>"` (Authorization page; RFC 9728 section 5.1) and no body.
- **A token that is no account and no live issued token:** **`401`** with `WWW-Authenticate: Bearer
  resource_metadata="<metadata URL>", error="invalid_token"` (RFC 6750 section 3.1). An issued token
  whose audience is another resource is the same `401` (the page's audience rule).
- **A token in the query string** (`access_token=`) is never read; the request is judged as having
  none.
- **Another `Authorization` scheme** (Basic, Digest) is **`400 Bad Request`**, no body.
- **A bearer token over plain `mcp`** is refused as an invalid one unless `--allow-plaintext-auth` is
  given (ADR-0032 section 3; as ADR-0110 decision 5).
- **`--allow-anonymous`** serves requests without a token as the anonymous caller, with the start-up
  warning ADR-0032 gives it. A request that carries a token is still checked, and a bad one is never
  served as anonymous.
- **Each refusal waits the failed-login delay** the authentication session already applies (ADR-0032).
- **`403` for insufficient scope** is never sent: Surl's accounts carry no scopes, and what a caller
  may do is decided by the option gates and share access, answered inside the tools (decision 2).

#### 6.2 The protected resource metadata (RFC 9728)

Served without authorization at `/.well-known/oauth-protected-resource<endpoint path>` (RFC 9728 section
3.1: the well-known segment inserted before the resource's path, so `/.well-known/oauth-protected-resource/mcp`),
`GET` only, `200` with `Content-Type: application/json`:

```json
{"resource":"<scheme>://<authority><endpoint path>","authorization_servers":["<scheme>://<authority>"],"bearer_methods_supported":["header"],"resource_name":"Surl MCP server"}
```

`<scheme>` is `http` for `mcp` and `https` for `mcps`; `<authority>` is the request's `Host` (the
server may be reached by several names; ADR-0108's virtual hosts do the same). That URL is the
`resource_metadata` of 6.1. `Origin` is checked as for the endpoint.

#### 6.3 The authorization server

The Authorization page requires `authorization_servers` to name at least one, and "nothing is left
out" (root `CLAUDE.md`): **the MCP server is its own OAuth 2.1 authorization server**, at the issuer
`<scheme>://<authority>`, built by **BL-722**:

- RFC 8414 metadata at `/.well-known/oauth-authorization-server`; `authorization_endpoint` `/authorize`,
  `token_endpoint` `/token`, `registration_endpoint` `/register` (RFC 7591 dynamic client registration,
  which the page says servers SHOULD support), `code_challenge_methods_supported` `["S256"]`,
  `grant_types_supported` `["authorization_code", "refresh_token", "client_credentials"]`.
- The authorization-code grant with PKCE `S256` required; `/authorize` authenticates the resource owner
  with HTTP Basic against Surl's accounts (a browser shows its own login prompt; no HTML form), and
  redirects only to the registered `redirect_uri`, which must be `https` or a loopback `http` URL.
- `client_credentials` for a non-interactive client, authenticating the client with an account name and
  password.
- Tokens are 32 random bytes base64url, opaque, bound to the `resource` parameter (RFC 8707, required by
  the page) and to the account, valid for 1 hour (refresh tokens 24 hours, rotated on use), held in
  memory only, and checked by 6.1 beside configured tokens.
- Until BL-722 lands, the metadata of 6.2 already names the issuer, and its endpoints answer `404`;
  configured tokens (`--user ':<token>'`) work from BL-466 on. BL-722 adds its own matrix rows (RFC 8414,
  RFC 7591, OAuth 2.1) to `McpServer.md`.

### 7. Errors and limits

#### 7.1 JSON-RPC error codes

Adopted from ADR-0103 decision 1's table, unchanged: `-32700`, `-32600` (also an over-limit message, a
request before `initialize`, a second `initialize`), `-32601`, `-32602` (unknown tool, schema violation,
bad cursor), `-32603` (an unexpected exception, with no exception text). `-32002` is never sent here,
since no resources are served. A tool failure is never a JSON-RPC error: it is decision 2's `isError`.

#### 7.2 HTTP statuses

| Case | Answer |
| --- | --- |
| Another path | `404`, no body |
| `Origin` refused | `403`, no body |
| No token / bad token (anonymous off) | `401` with decision 6.1's challenge, no body |
| Another `Authorization` scheme | `400`, no body |
| `GET` or another method on the endpoint | `405` with `Allow: POST, DELETE` |
| `Accept` missing a type | `406`, no body |
| `Content-Type` not JSON | `415`, no body |
| No `Mcp-Session-Id` after initialization; one on `initialize`; bad `MCP-Protocol-Version` | `400`, no body |
| Unknown or expired session | `404`, no body |
| Body the core refuses | `400` with the core's JSON-RPC error, `id` null |
| Body past the message limit | `413 Content Too Large`, no body, then close |
| Session bound reached | `503` with `Retry-After: 60` |
| Notification or response | `202`, no body |
| Request | `200`, JSON or (for `tools/call`) SSE |
| `DELETE` of a session | `204` |

#### 7.3 Limits (ADR-0006)

| Limit | How the server applies it | Answer when hit |
| --- | --- | --- |
| `--max-message` (1 MiB) | one JSON-RPC message: an HTTP body, checked against `Content-Length` before reading and while reading a chunked body; a stdio line, passed to the core in place of its 4 MiB default (ADR-0103 decision 1) | HTTP `413` then close; stdio the core's `-32600` with a null `id`, the session continuing |
| a response | `read_file`'s answer is bounded by `--max-message` too (decision 2) | `isError` |
| `--max-filesize` (100 MiB) | `write_file`'s decoded bytes | `isError` |
| `--max-request-head` (100 KiB) | `Surl.HttpServing`'s head reader | its answer, then close |
| sessions | 1000 a server, 30 minutes idle (decision 4.5) | `503` |
| requests in flight | the core's 16 per stdio session; on HTTP, 16 per session (a 17th `POST` waits for one to finish) | back-pressure, no refusal |
| `--head-timeout`, `--idle-timeout`, `--max-time` | `Surl.HttpServing`'s and `Surl.Core`'s, as for HTTP; an SSE stream's keep-alive comments count as traffic | close with no bytes |
| `--max-connections`, `--max-connections-per-address` | `Surl.Core`'s, through the server's refusal writer | `503`, no body, then close |
| listing | 1000 entries (decision 2) | truncated, flagged |

No new option is added. The 1000 sessions and 30 minutes are fixed: like ADR-0097's per-flow bounds they
size memory, not behaviour a client sees in normal use.

### 8. `--aihelp` and the command line

The topic is **`mcp`** (ADR-0083 decision 8), help category `mcp`, schemes `mcp`, `mcps` and
`mcp+stdio`. Its about text says the server answers Model Context Protocol 2025-06-18 clients with
tools over the shared content store; that it is judged by the MCP specification and not by upstream
curl; that `mcp`/`mcps` require a Bearer token (`--user ':<token>'`) unless `--allow-anonymous`, at
the endpoint `/mcp` by default; and that `mcp+stdio:` serves standard input and output and must be the
only listen URL. BL-468 writes it with its example.

## Alternatives considered

- **SSE for every request, or JSON for every request.** JSON for all would leave a long `tools/call`
  exposed to idle timeouts with no way to keep the connection alive; SSE for all costs every short
  answer a stream. Fixed by method (4.3) is simple and testable.
- **A server-to-client `GET` stream with event IDs and resumption.** Rejected while the server sends
  nothing unsolicited (decision 1); a later feature that sends notifications (`list_changed`, progress)
  would amend 4.4 and 4.8.
- **Refusing a request without `Origin`.** Rejected: every non-browser client would fail, and the
  page's threat (DNS rebinding) needs a browser, which sends it.
- **Bearer tokens without an authorization server**, metadata naming none. Rejected: the page requires
  at least one in `authorization_servers`; decision 6.3 builds it rather than leaving the field false.
- **Serving files as MCP resources** (`resources/list`, `resources/read`) instead of, or beside, tools.
  Not taken: the task plans tools, the port's client reads tools only, and resources would add a second
  path to the same files. A later task may add a resource provider; the core already supports one.
- **Writes without `--allow-uploads`, refused inside the tool.** Rejected for not registering the
  tool: an agent is never offered a tool it cannot use, and ADR-0006 keeps uploads off by default.
- **`mcp+stdio:` combined with network listen URLs.** Rejected (decision 5): the session's end is the
  process's end.
- **Authorization on stdio** (a token in an environment variable). Not taken: the page says stdio does
  not follow the HTTP authorization flow, and the process owner already chose its options.

## Consequences

Good:

- One MCP core serves this server and `surl-mcp`, so a fix to JSON-RPC handling reaches both.
- A default `mcp`/`mcps` server reveals nothing without a token, and a default server of any transport
  reads files only: listing and writing each need their own option.
- Every refusal is a fixed row, so tests pin each byte from the pages.

Costs and caveats:

- The port's client sends no `Authorization`, so BL-469's integration exchanges run with
  `--allow-anonymous` and prove the `401` separately.
- Until BL-722, the authorization server's endpoints answer `404`, and only configured tokens work.
- The matrix is `Documentation/Conformance/Extended/McpServer.md` (ADR-0083 decision 9).
- Follow-up: **BL-722** (the authorization server, decision 6.3), filed with this ADR.
