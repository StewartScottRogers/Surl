# Surl.Extended.Protocol.McpServer.UnitLibrary

An extended server (ADR-0083): it will answer the `mcp`, `mcps` and `mcp+stdio` schemes the Curl
port adds, which upstream curl does not have. ADR-0111 decides how. It is judged by the MCP
specification (revision 2025-06-18 and the two before it), never by the Curl port's output, and its
unit tests replay messages written by hand from that specification's examples.

It references `Surl.Protocol.Abstractions`, the shared MCP core `Surl.McpServing` (ADR-0103), `Surl.HttpServing` and
`Surl.Content` (the tools' files, ADR-0111 decision 1).
JSON-RPC 2.0 parsing and writing, the lifecycle, version negotiation and the error codes are the
core's; never write them here.

What is here so far is the lifecycle (BL-464, ADR-0111 decisions 1, 3 and 7):

- `McpServerIdentity` is this server's `serverInfo` (`surl`, `Surl MCP server`, surl's version
  given by the host) and its `instructions`.
- `McpServerSession` is one transport-independent session: it gives the core that identity and the
  server's registry, and the core answers `initialize`, `notifications/initialized`, `ping`, refuses
  requests before `initialize`, and writes the JSON-RPC errors. `CreateOptions` makes the options
  every session of the server shares, with `--max-message` as the message limit.

The tools (BL-465, ADR-0111 decision 2): `McpServerTools.CreateRegistry(store, caller, maximumMessageBytes)`
builds the registry one session serves, so every file operation runs as that session's `ContentCaller`. In
`tools/list` order: `McpListDirectoryTool` (`list_directory`, only under `--list-directories`; at most 1000
entries, then `...` and `is_truncated`), `McpReadFileTool` (`read_file`: well-formed UTF-8 as one text
block, anything else as one embedded resource `file:///<path>`, `application/octet-stream`; a file whose
length, escaped text or base64 passes the message limit is refused), `McpWriteFileTool` (`write_file`, only
under `--allow-uploads`; exactly one of `text` and `base64`, written through `ContentStore.WriteUploadAsync`)
and `McpEchoTool` (`echo`). `McpToolPath` drops one leading `/` and percent-encodes each segment, so the
content store sees exactly the characters given and applies its own refusals. Every failure inside a tool is
an `isError` result with a fixed `McpToolFailureText`; argument errors and unknown tools are the core's
`-32602`. A dot-file or a share the caller may not read is answered `no such file or folder`, as the content
store answers it as absent.

Streamable HTTP (BL-466, ADR-0111 decisions 4, 6 and 7), on `Surl.HttpServing`'s `Http1ConnectionServer`:

- `McpProtocolServer` (schemes `mcp` and `mcps`) takes the content store, the `IAuthenticationPolicy`, surl's
  version, a `TimeProvider`, a `RandomNumberGenerator` for session IDs and the authorization server's client IDs,
  codes and tokens, and the listen URL's path (`ListenUrl` carries none, so the host passes it; empty or `/` is
  `/mcp`).
- `McpHttpRequestAnswerer` answers each request in decision 4.2's order: path (the endpoint,
  `/.well-known/oauth-protected-resource<endpoint>` served by `McpProtectedResourceMetadata`, or an authorization
  server path, below), `Origin` (`McpOriginCheck`), the login (`Bearer` only; a live token the authorization server
  issued for this resource logs in as its account, any other is judged by the policy's session; a refusal is `401`
  with `resource_metadata`, plus `error="invalid_token"` when a token was sent), the method (`GET` and others `405 Allow: POST, DELETE`),
  `Accept` (`McpAcceptField`) and `Content-Type`, the body (`McpMessageBuffer`, bounded by `--max-message`:
  `413` and close), then the session and `MCP-Protocol-Version`.
- `McpHttpSessionTable` issues `Mcp-Session-Id` (32 random bytes, base64url) once the core answers
  `initialize`, binds it to the login, expires it after 30 idle minutes and holds at most 1000 (`503
  Retry-After: 60` past that). `McpHttpSession` runs at most 16 requests at once, cancels on
  `notifications/cancelled` and on `DELETE`.
- `tools/call` is answered as `text/event-stream` with one `data:` event (`McpEventStream`); every other
  request as `application/json`. The event is written as one `Content-Length` body: the 15-second
  `: keep-alive` comments of decision 4.3 need a streaming answer body in `Surl.HttpServing` first (see the
  task board).

The OAuth 2.1 authorization server (BL-722, ADR-0111 decisions 6.3 and 6.4), at the issuer
`<scheme>://<authority>`. Every answer is written by `McpHttpAnswerWriter`; `McpHttpFields` reads header fields.

- `McpOAuthJson` writes the RFC 8414 document (`/.well-known/oauth-authorization-server`), registrations, token
  responses and error objects, and names the paths.
- `McpClientRegistrationEndpoint` (`POST /register`, RFC 7591) registers public clients whose redirect URIs pass
  `McpRedirectUri` (`https` or loopback `http`, no fragment).
- `McpAuthorizeEndpoint` (`GET /authorize`) checks the client and redirect URI (`400`, never redirected), then
  `response_type`, PKCE `S256` (`McpPkce`) and `resource`, then the owner's HTTP Basic login, and redirects the code.
- `McpTokenEndpoint` (`POST /token`, form body read by `McpOAuthForm`) serves `authorization_code`,
  `refresh_token` (rotated) and `client_credentials`.
- `McpPasswordCheck` checks every password through `IAuthenticationPolicy.CheckPasswordLoginAsync`, never the HTTP
  session, whose methods on an MCP server are Bearer only.
- `McpAuthorizationGrants` holds clients, codes (10 minutes, used once), access tokens (1 hour) and refresh tokens
  (24 hours) in memory, each in an `McpExpiringTable` of at most 1000 live entries keyed by 32 random bytes,
  base64url. An access token is accepted only at the resource (RFC 8707) it was issued for.

stdio (BL-467, ADR-0111 decision 5's library half): `McpStdioSession.RunAsync(input, output, store, surlVersion,
maximumMessageBytes, cancellationToken)` serves one session over the stream pair it is given through the core's
`McpStreamSession` (newline framing, the line limit, cancellation and ending on a closed input are the core's), with
this server's identity and tools run as the anonymous caller on scheme `mcp+stdio`. It never touches `Console`.

Still to come: the command line (BL-468), which binds `McpStdioSession` to surl's standard streams.

**Chosen-error fault** (ADR-0144, BL-835): `McpHttpRequestAnswerer` offers every JSON-RPC request inside a session
to `McpRequestFaults` before the core, with `account` the login, `path` the endpoint path and `command` the tool's
`name` for `tools/call`, the method otherwise. A matching `error` rule whose reply is `<code> <message>` is answered
with the core's `JsonRpcResponseWriter.WriteError`, in the request's usual media type, publishing
`FaultAppliedEvent`; `initialize`, notifications and responses are never offered. `Surl.McpServing` is untouched.
