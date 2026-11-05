# ADR-0103 — Surl.Mcp, its tools, and the MCP core it shares with the extended MCP server

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-612. Stewart decided that `Surl.Mcp` exists
  ([ADR-0098](ADR-0098-surl-api-and-surl-mcp-join-surl-as-products.md), BL-610); this ADR decides its
  design.
- **Builds on:** [ADR-0098](ADR-0098-surl-api-and-surl-mcp-join-surl-as-products.md) (the hosts exist),
  [ADR-0099](ADR-0099-one-server-composition-for-surl-and-surl-api-and-surl-apis-programming-interface.md)
  (`Surl.Composition` and `Surl.Api`, which the tools drive; its decision 7 is the exchange record),
  [ADR-0003](ADR-0003-upstream-curl-is-surls-only-oracle.md) (upstream curl is the only oracle),
  ADR-0032 (secure by default), ADR-0033 (log levels), ADR-0046 (`--aihelp`), ADR-0079 (category folders),
  ADR-0083 (the extended servers, `SURL_EXTENSIONS`) and ADR-0094 (Unix domain socket listen URLs).
- **Amends:** [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md) decision 3's table, which gains
  `Surl.McpServing.UnitLibrary` (decision 1).
- **Binds:** BL-463's decisions 3 (version negotiation) and 7 (JSON-RPC error codes and the message limit)
  for the extended MCP server.
- **Acted on by:** BL-619, BL-620, BL-621, BL-623, BL-624, BL-625, BL-626, BL-627, BL-628, BL-632, BL-637,
  BL-643 and BL-649, and the amended BL-463, BL-464, BL-465, BL-467 and BL-468 (listed under Consequences).

## Context

ADR-0098 makes `Surl.Mcp` a product: an MCP server on standard input and output whose tools let an AI
agent start, list and stop Surl servers and see what a client sent. The extended MCP protocol server,
`Surl.Extended.Protocol.McpServer` (BL-463 to BL-469), answers `mcp`, `mcps` and `mcp+stdio` and needs the
same machinery: JSON-RPC 2.0, the MCP lifecycle, tools, pagination and the stdio framing. Building that
twice would let the two drift.

The specification is MCP revision 2025-06-18, https://modelcontextprotocol.io/specification/2025-06-18,
the revision BL-463 plans for. The pages this ADR relies on:

- "Base Protocol > Overview" (basic/index): JSON-RPC 2.0 messages; a request `id` is a string or an
  integer and never null; responses carry `result` or `error`; notifications get no response; this
  revision removed JSON-RPC batching.
- "Base Protocol > Lifecycle" (basic/lifecycle): `initialize` with `protocolVersion`, `capabilities` and
  `clientInfo`; the result's `protocolVersion`, `capabilities`, `serverInfo` and optional `instructions`;
  "If the server supports the requested protocol version, it MUST respond with the same version.
  Otherwise, the server MUST respond with another protocol version it supports"; the client "SHOULD NOT
  send requests other than pings before the server has responded to the `initialize` request";
  `notifications/initialized`; for stdio, shutdown is the client closing the server's input.
- "Base Protocol > Transports > stdio" (basic/transports): messages are delimited by newlines and "MUST NOT
  contain embedded newlines"; the server "MUST NOT write anything to its `stdout` that is not a valid MCP
  message" and "MAY write UTF-8 strings to its standard error".
- "Server Features > Tools" (server/tools): `tools/list`, `tools/call`, `inputSchema`, `content`,
  `structuredContent`, `isError`, and "Error Handling": unknown tools and invalid arguments are protocol
  errors (`-32602` in the page's example), failures inside a tool are results with `isError: true`.
- "Server Features > Resources" (server/resources): `resources/list`, `resources/read`, `uri`, `name`,
  `mimeType`, `text`, and "Error Handling": resource not found is `-32002`, an internal error `-32603`.
- "Utilities > Ping", "Utilities > Pagination" (an opaque `cursor`, `nextCursor`; an invalid cursor is
  `-32602`) and "Utilities > Cancellation" (`notifications/cancelled` with `requestId`; the receiver
  "SHOULD NOT send a response for the cancelled request").
- JSON-RPC 2.0, https://www.jsonrpc.org/specification: `-32700` parse error, `-32600` invalid request,
  `-32601` method not found, `-32602` invalid params, `-32603` internal error.

The Curl port's `Curl.Mcp`, `Curl.Mcp.UnitLibrary` and `Curl.Mcp.Protocol.UnitLibrary`, and its ADR-0548,
ADR-0596, ADR-0799, ADR-0800 and ADR-0801, were read for shape and are copied where they fit. They are
not evidence for anything here (ADR-0003); the reasons below are Surl's and the specification's.

Constraints: base class library only (`System.Text.Json`'s `Utf8JsonReader`, `Utf8JsonWriter`,
`JsonDocument` and source-generated contexts; no reflection-based serialization); native AOT, so no
assembly scanning and no attributes read at run time; the four quality gates; MSTest only; tests pass on
Windows, Linux and macOS; `TimeProvider` injected; publishing a package or a release needs Stewart.

## Decision

### 1. The shared MCP core: `Surl.McpServing`

A new horizontal library `Protocols/Http/Surl.McpServing.UnitLibrary` (namespace `Surl.McpServing`), tested
by `Protocols/Http/Surl.McpServing.UnitTests`. It sits in `Protocols/Http/` beside the extended MCP server
that shares it, as `Surl.HttpServing` and `Surl.WebSocket` sit beside theirs; no new category folder is
made. **It references nothing**: it takes `Stream`s and `JsonElement`s, and needs nothing from
`Surl.Protocol.Abstractions`. ADR-0002 decision 3's table gains the row
`Surl.McpServing.UnitLibrary` (ADR-0103) | nothing, and `ProtocolIsolationTests.HorizontalLibraries` gains
it (BL-619). `Application/Surl.Mcp.UnitLibrary` and `Surl.Extended.Protocol.McpServer` reference it.

It holds:

- **JSON-RPC 2.0 messages.** Reading follows the port's ADR-0800 rules, adopted: a line that is not UTF-8,
  not JSON or nested deeper than 64 levels is `-32700` with a null `id`; JSON that is not an object (a
  batch array included) is `-32600` with a null `id`; an `id` that is null, a boolean, an object or an
  array is `-32600` with a null `id`; a string or number `id` is echoed byte for byte; `jsonrpc` other than
  the string `"2.0"`, a missing or non-string `method`, or `params` that is not an object, array or null is
  `-32600`, echoing the `id` when one was read; `"params": null` is absent; unknown members are ignored; a
  key given twice takes its last value. `params` and `result` travel as `JsonElement`. Writing uses one
  source-generated, serialization-only `JsonSerializerContext` and `Utf8JsonWriter`; a response is one line
  with no embedded newline (every `\n` in a string is written escaped).
- **The error codes.** Every failure the core answers, and the code the extended server answers too:

  | Failure | Code | `message` |
  | --- | --- | --- |
  | Not UTF-8, not JSON, too deep | `-32700` | `Parse error` |
  | Not an object, a batch, a bad `id`, `jsonrpc`, `method` or `params` | `-32600` | `Invalid Request` |
  | A message over the size limit | `-32600` | `Invalid Request: the message is longer than <n> bytes` |
  | A request other than `initialize` or `ping` before `initialize` was answered | `-32600` | `Invalid Request: the session is not initialized` |
  | A second `initialize` | `-32600` | `Invalid Request: the session is already initialized` |
  | An unknown method | `-32601` | `Method not found` |
  | `initialize` without a string `protocolVersion` or an object `capabilities` | `-32602` | `Invalid params: <what is missing>` |
  | An unknown tool, arguments that break the tool's schema, a bad cursor, a missing `uri` | `-32602` | `Invalid params: <which>` |
  | A resource that does not exist | `-32002` | `Resource not found` (with `data.uri`) |
  | A handler that throws anything but `McpToolFailureException` or a cancellation | `-32603` | `Internal error` (no exception text, so no path or secret leaks) |

  An unknown notification is ignored, as JSON-RPC requires no answer to one. A response or error the
  client sends (the server makes no requests) is ignored.
- **The lifecycle and version negotiation.** The core accepts the revisions `2025-06-18`, `2025-03-26`
  and `2024-11-05`. A requested version on that list is answered with the same version; any other string is
  answered, as the Lifecycle page requires, with `2025-06-18`, the latest the core supports, and the client
  decides whether to disconnect - an unsupported version is never a JSON-RPC error. The result carries the
  caller's `serverInfo` (`name`, `title`, `version`), the caller's `instructions` when it gives one, and
  the capabilities the registry earns: `tools` (`{"listChanged": false}`) only when a tool is registered,
  `resources` (`{"subscribe": false, "listChanged": false}`) only when a provider is registered. No
  `logging`, `prompts` or `completions` capability is advertised. `ping` is answered `{}` before and after
  `initialize`. Requests are accepted once `initialize` is answered, whether or not
  `notifications/initialized` has arrived, since the page restricts only what the server sends before it.
  The same wire shapes are written for every accepted version; `structuredContent` and `title` are written
  only when `2025-06-18` was negotiated, because the two older revisions do not define them. Batching is
  refused in every version, including `2025-03-26`, which defined it: no client the project measures
  batches, and one code path is simpler. This is a departure from `2025-03-26`, recorded here.
- **The message limit.** One message (one line, newline excluded) is at most 4 MiB by default; the caller
  may set another limit. A longer line is read to its newline and discarded without being buffered, then
  answered `-32600` with a null `id`, and the session continues. The extended server passes its
  `--max-message` value instead (BL-463 decision 7).
- **The tool model and registry.** Port ADR-0801 decisions 1, 3 and 4, adopted. A tool is an
  `McpTool` record - `name`, `title`, `description`, the input schema as hand-written JSON text, and an
  async handler `(JsonElement arguments, CancellationToken) -> Task<McpToolResult>` - added with one
  explicit `McpServerRegistry.AddTool` call each; there is no scanning and no attribute. `AddTool` throws
  `ArgumentException` for a duplicate name (the first stays registered), for schema text that is not JSON,
  or for a schema that is not an object whose `type` is `"object"`. `tools/list` lists in registration
  order.
- **Argument checking.** The core checks `tools/call` arguments against the schema before the handler
  runs, so no tool repeats it. It supports exactly this subset of JSON Schema, and `AddTool` refuses a
  schema using any other keyword, so a check can never be silently skipped: `type` (`object`, `string`,
  `integer`, `number`, `boolean`, `array`), `properties`, `required`, `additionalProperties: false`,
  `items`, `enum` (strings), `minimum`, `maximum`, `minItems`, `maxItems`, `minLength`, `maxLength`,
  `description`, `default` (documentation only) and `title`. A violation is `-32602` naming the first
  failing path (`arguments.listen_urls[0]: expected a string`). Missing `arguments` is the empty object.
- **Results.** `McpToolResult` holds `content` (text blocks only; no tool here returns images or audio),
  an optional `structuredContent` object and `isError`, always written. A handler reports a failure inside
  the tool by returning an `isError` result or throwing `McpToolFailureException(message)`, which becomes
  `isError: true` with that message as its one text block. A handler cancelled through its token gets no
  response.
- **Resources.** Port ADR-0801 decision 4, adopted: an `IMcpResourceProvider` lists a fixed set of
  resources (`uri`, `name`, `title`, `mimeType`, `description`), read once when added with
  `McpServerRegistry.AddResources`, and reads one by URI, returning null for not found; a provider any of
  whose URIs is already registered or listed twice throws `ArgumentException` and adds none. `resources/read`
  answers `contents` with one `text` entry; null is `-32002`. Resource templates are not served.
- **Pagination.** `tools/list` and `resources/list` page at 100 entries by default (the caller may set
  another size). The cursor is opaque to the client: the next offset as invariant decimal text, base64url
  encoded without padding. A cursor that does not decode to an offset greater than 0 and less than the
  count is `-32602 Invalid params: cursor`. `nextCursor` is omitted on the last page.
- **The stdio session over a stream pair.** `McpStreamSession.RunAsync(Stream input, Stream output,
  McpServerRegistry registry, McpSessionOptions options, CancellationToken)` reads newline-delimited
  messages (a trailing `\r` before the `\n` is dropped; an empty line is ignored), and writes each message
  as one line ending in `\n`, one whole line at a time behind one writer lock, flushing after each. It
  never touches `Console`. Up to 16 requests run at once; the 17th waits to be read until one finishes, so
  input is back-pressured rather than buffered. `notifications/cancelled` cancels the named request's
  token. `RunAsync` returns when the input ends (end of stream) or the token is cancelled, after
  cancelling the handlers still running and waiting for them to return; responses to them are not written.
  The extended server's `mcp+stdio` and `Surl.Mcp` both bind it to real standard streams; the Streamable
  HTTP transport, its sessions and `Origin` check, and the extended server's own tools stay in
  `Surl.Extended.Protocol.McpServer`, which feeds the same request dispatcher one message at a time.

### 2. The host's projects

- `Application/Surl.Mcp` - the executable, **assembly `surl-mcp`**, namespace `Surl.Mcp`. A naming
  exception like `Surl.Console` (ADR-0098). It holds `Program` (signals, `Program.RunAsync(input, output,
  error, arguments, environment, cancellationToken)`, which tests call with in-memory streams), the
  `surl-mcp` command line of decision 4, and the registration of each tool and resource provider with one
  explicit line. Its csproj mirrors `Surl.Console`'s: `OutputType` `Exe`, `PublishAot` on,
  `InvariantGlobalization`.
- `Application/Surl.Mcp.UnitLibrary` - namespace `Surl.Mcp`. All tool logic: the tools, their schemas and
  descriptions, the running-server set, the safety checks and the byte rendering. It references `Surl.Api`
  (the tools drive `Surl.Api`'s `SurlServer`; this answers ADR-0099 decision 1's open point - `Surl.Mcp`
  composes through `Surl.Api`, never `Surl.Composition` directly), `Surl.Cli` (the `--aihelp` text and the
  parsed `SurlCommandLine`) and `Surl.McpServing`.
- `Application/Surl.Mcp.UnitTests` - tests both, as the port's `Curl.Mcp.UnitTests` does. Both production
  projects are held to the four quality gates.

All three are composition roots (ADR-0099 decision 11): no library outside `Application/` references
them, and `Surl.Mcp.UnitLibrary` never references `Surl.Mcp`.

### 3. The tools

Four tools, in this `tools/list` order. Names are snake_case; every property name in schemas and results
is snake_case. Every result carries its object as `structuredContent` and the same object serialized as
the one text block (the Tools page's backwards-compatibility rule), except where a failure is stated.

**`start_server`** - "Start a Surl protocol server that listens on the given URLs and answers clients as
`surl` with the same options would. Returns its ID and the URLs it bound. Use port 0 to let the system
choose a free port. Read the `surl://aihelp` resources for every scheme and option."

```json
{
  "type": "object",
  "properties": {
    "listen_urls": { "type": "array", "items": { "type": "string" }, "minItems": 1, "maxItems": 16,
      "description": "What to listen on, as surl's listen URLs, e.g. http://127.0.0.1:0" },
    "arguments":   { "type": "array", "items": { "type": "string" }, "maxItems": 256,
      "description": "Any other surl options, as surl's own words, e.g. [\"--user\", \"alice:secret\"]" },
    "files":        { "type": "object", "description": "In-memory content: '/'-separated relative path -> UTF-8 text" },
    "files_base64": { "type": "object", "description": "In-memory content: '/'-separated relative path -> base64 bytes" }
  },
  "required": ["listen_urls"],
  "additionalProperties": false
}
```

It builds `SurlServerOptions` - `ListenUrls`, `AdditionalArguments` from `arguments`, `Files` from `files`
and `files_base64` (a path in both is refused) - with `RecordExchanges` on, `MaxRecordedExchanges` 100,
`MaxRecordedBytesPerExchange` 262144 (so eight servers hold at most 200 MiB), `Log` a writer that copies
each line to standard error and keeps the lines written during the start, and `LogLevel` the host's. It
applies decision 4 first, then calls `SurlServer.StartAsync`. Typed properties beyond these are not
offered: `surl`'s words are the one vocabulary, documented in the resources, and `Surl.Api` parses them
exactly as `surl` does (ADR-0099 decision 4).

Result: `{"server_id": "s1", "listen_urls": [...], "bound_urls": [...], "log_lines": [...]}`. Server IDs
are `s` and a decimal counter from 1, never reused in the process's life.

Failures, each `isError: true`:

- `SurlServerStartException` - every refusal `surl` makes (exit codes 1, 2, 6, 37, 45, 58, 77, 124):
  `structuredContent` `{"exit_code": 58, "exit_code_name": "CertificateProblem", "lines": [...]}` (the name
  the `SurlExitCode` member's), and the text block each line prefixed `surl: `, as `surl` writes them.
- `ArgumentException` from `Surl.Api` (an option a host owns, a bad `Files` path): its message, no
  `exit_code`.
- A decision-4 refusal: the text `refused: <rule>`, `structuredContent` `{"refused": "<rule>"}`.

**`list_servers`** - "List the Surl servers this host started and that are still running."
Schema `{"type": "object", "properties": {}, "additionalProperties": false}`. Result
`{"servers": [{"server_id", "listen_urls", "bound_urls", "started_at", "exchange_count",
"dropped_exchange_count"}]}` in start order; `started_at` is ISO 8601 UTC from the injected
`TimeProvider`. Never an error.

**`stop_server`** - "Stop a Surl server this host started and release its ports." Schema: `server_id`
(string, required), `additionalProperties: false`. Result `{"server_id", "exit_code", "exit_code_name"}`
from `SurlServer.StopAsync` (`0 Ok`, or `125 InternalError` when serving had failed). An ID that names no
running server is `isError: true`, `no running server has ID <id>`. A stopped server's exchanges are gone
with it.

**`get_exchanges`** - "Show what each client sent to a Surl server and what the server sent back, byte
for byte, oldest first." Schema: `server_id` (string, required), `after_exchange_id` (integer, minimum 0,
optional: only exchanges with a larger ID), `max_exchanges` (integer, 1 to 100, default 20),
`additionalProperties: false`. Result:

```json
{
  "server_id": "s1",
  "exchanges": [
    {
      "exchange_id": 1,
      "client": "127.0.0.1:50512",
      "started_at": "2026-11-04T10:00:00.0000000+00:00",
      "chunks": [
        { "direction": "received", "encoding": "text",   "data": "GET / HTTP/1.1\r\n..." },
        { "direction": "sent",     "encoding": "base64", "data": "..." }
      ],
      "recorded_bytes": 1234,
      "shown_bytes": 1234,
      "is_cut": false,
      "is_truncated": false
    }
  ],
  "dropped_exchange_count": 0,
  "has_more": false
}
```

- **Text or base64, per chunk.** A chunk is `text` when its bytes are well-formed UTF-8 and hold no
  control character but tab, CR and LF; otherwise it is `base64`. JSON escaping keeps the text one line.
- **The cap.** At most 65536 recorded bytes of each exchange and 1 MiB in the whole result are shown. A
  cut ends a chunk early (backing off to the start of a UTF-8 sequence so a text chunk stays well-formed)
  and drops the chunks after it; `is_cut` is true and `shown_bytes` says how much was shown.
  `is_truncated` is `Surl.Api`'s own flag: the record itself stopped at its per-exchange bound. Once the
  1 MiB is reached, later exchanges are left out and `has_more` is true; the agent asks again with
  `after_exchange_id`.
- **No listen URL per exchange.** ADR-0099 decision 7 records none; an agent serving several listen URLs
  on one server tells them apart by what was sent, or starts one server per URL.
- An unknown ID is `isError: true` as for `stop_server`.

### 4. Safety

The command line's secure-by-default rules (ADR-0032) apply unchanged, because every option is parsed by
`Surl.Cli` exactly as `surl` parses it: no anonymous login, no plaintext authentication, no uploads, no
listings unless the agent passes the option that loosens each, and its start-up warning appears in
`log_lines` and on standard error. On top of that, `surl-mcp` confines what an agent can reach outside the
process. `start_server` parses `listen_urls` and `arguments` with `CommandLineParser` first and checks the
parsed `SurlCommandLine`, so a listen URL given as a positional word in `arguments` (or through `--url`
once BL-644 adds it) is checked the same way.

- **Loopback only by default.** Every listen URL's host must be `localhost`, an IPv4 address in
  `127.0.0.0/8` or `::1`; a Unix domain socket listen URL (ADR-0094) counts as local and its path is
  checked as a path (below). Anything else - a name, `0.0.0.0`, `::`, a LAN address - is refused unless
  `surl-mcp` was started with `--allow-non-loopback`. The agent cannot widen this; only whoever starts the
  host can.
- **At most 8 servers at once** by default; `--max-servers <n>` (1 to 64) changes it. A start past it is
  refused naming the limit.
- **Local paths are confined to a root.** The root is `--root <dir>`, else the environment variable
  `SURL_MCP_ROOT`; it must exist when `surl-mcp` starts (else exit 2). **With no root, an option that
  names a local path is refused**, so a default host can serve only in-memory `files`. With a root, every
  path - the data directory (`--directory`), `--user-file`, `--cert`, `--key`, `--cacert`, `--hostkey`,
  `--hostcert`, `--authorized-keys`, `--keytab`, layer-2 item paths (ADR-0101) and a Unix socket path - is
  taken relative to the root when relative, and must lie inside it once `..` and symbolic links are
  resolved; anything else is refused naming the option. Which `SurlCommandLine` member holds a path is a
  table in `Surl.Mcp.UnitLibrary`; a test enumerates `SurlCommandLine`'s members by reflection (tests are
  exempt from AOT) and fails on one the table does not classify, so a new path option cannot slip past.
- **Options refused outright.** `-K`/`--config` (a config file could name paths the check never sees),
  plus what `Surl.Api` already refuses as an `ArgumentException`: `--help`, `--aihelp`, `--manual`,
  `--version`, `--log-file`, `--trace`, `--trace-ascii`.
- **`SURL_EXTENSIONS`** is the host process's, as for `surl`; the agent cannot change it.
- **The data-directory lock still holds** (ADR-0031 decision 7): two servers on one directory refuse with
  124, as two `surl`s do.

### 5. Lifetime and logging

`surl-mcp` serves one session on its own standard input and output through `McpStreamSession`. When the
input ends, or on Ctrl+C or SIGTERM, it stops every server it started (all at once, each through
`StopAsync`, so each gets `surl`'s shutdown grace period), and exits 0. Standard output carries only MCP
messages. Every log line - the host's own, and each server's start-up warnings, status lines and
exchange log at `-v` and above - goes to standard error at the level `--log-level` (surl's words,
ADR-0033; default surl's default) chooses. A server that stops serving on its own (a fatal serving error)
stays listed until `stop_server` collects its exit code.

The `surl-mcp` command line is `surl-mcp [--root <dir>] [--allow-non-loopback] [--max-servers <n>]
[--log-level <level>]`, plus `--help` and `--version`, which print to standard output and exit 0 without
serving. Any other word is refused on standard error with exit 2 (`FailedInit`, as `surl`'s own usage
errors).

### 6. Agent-facing help

- **The tools describe themselves**: decision 3's descriptions and schemas, with every property described.
- **`surl --aihelp` is served as MCP resources** from `Surl.Cli`'s `AiHelpText.Answer`, never copied or
  re-worded: `surl://aihelp` (the overview, `Answer(null)`), `surl://aihelp/<topic>` for each entry of
  `AiHelpTopics.All` in its order, and `surl://aihelp/all`; `mimeType` `text/markdown`; `name` the
  `surl --aihelp ...` command that prints it. A completeness test enumerates `AiHelpTopics.All` and fails
  when a topic has no resource (BL-627).
- **`initialize`'s `instructions`** says in two sentences that the tools start Surl servers on loopback
  and that `surl://aihelp` explains the listen URLs and options `start_server` takes.
- **`serverInfo`** is `{"name": "surl-mcp", "title": "Surl", "version": <surl's informational version>}`.
- **`surl --aihelp`'s overview points at `Surl.Mcp`** in one sentence (BL-628, already planned by
  ADR-0099 decision 9). No `--aihelp` topic describes `Surl.Mcp`; its tools and resources are its help.

### 7. Shipping

- **Native AOT archives.** `.github/workflows/release.yml` publishes `Surl.Mcp` native AOT for each runtime
  identifier it publishes `surl` for, smoke-tests it by answering `initialize` over standard input and
  output, and packages it as its own archive, `surl-mcp-<version>-<rid>` (`.zip` on Windows, `.tar.gz`
  elsewhere), beside `surl`'s. `DOWNLOAD.md` lists them (BL-637).
- **A NuGet MCP server package, dry run only.** `Surl.Mcp` is packable as one framework-dependent .NET
  tool package, ID `Surl.Mcp`, `PackageType` `McpServer`, `ToolCommandName` `surl-mcp`, with
  `.mcp/server.json` naming the package and its stdio transport; a test holds the csproj's version and
  `server.json`'s equal. One framework-dependent package runs on every platform `dotnet` runs on, so there
  is no per-runtime package matrix; the native binary is what the archives are for. Its version is the one
  `release.yml` gives `surl`. `release.yml` packs it and keeps the `.nupkg` as a workflow artifact (BL-643).
- **Pushing either is Stewart's** (root `CLAUDE.md`, "Git and GitHub"); no task plans the push.

### 8. The extended MCP server builds on the core

Confirmed: `Surl.Extended.Protocol.McpServer` references `Surl.McpServing` and uses its messages,
lifecycle, error codes, message limit, registry, pagination and stream session, as BL-464, BL-465 and
BL-467 now assume. BL-463 adopts decision 1's versions, unsupported-version answer, error codes and limit
for its decisions 3 and 7, and decides only the server's own project, tools, Streamable HTTP, how
`mcp+stdio` fits the command line, authorization and limits.

## Consequences

- One MCP implementation serves both hosts, so a fix to JSON-RPC handling or pagination reaches the
  extended server and `surl-mcp` together.
- A server an agent starts answers the same bytes as `surl` with the same options, because `Surl.Api`
  composes it from the same parsed command line (ADR-0099); BL-632 proves it with pinned upstream curl
  8.21.0. Upstream curl stays the only oracle; nothing an MCP client does is evidence.
- A default `surl-mcp` can reach no file and no non-loopback address: an agent serves in-memory files on
  loopback until a person starts the host with a root or `--allow-non-loopback`.
- The follow-up tasks, filed with BL-612, and where this ADR departs from the planning assumptions in
  BL-612:
  - **BL-619** creates `Surl.McpServing` with the messages, error table, lifecycle, versions and the 4 MiB
    limit of decision 1. It references nothing (the planning allowed Abstractions).
  - **BL-620** adds the tool registry, the schema subset check and pagination at 100. Departure: a handler
    failure is `isError` only through `McpToolFailureException` or an `isError` result; any other exception
    is `-32603`.
  - **BL-621** adds `McpStreamSession` with 16 concurrent requests and back-pressure.
  - **BL-623** adds resources (port ADR-0801 decision 4 adopted; no templates).
  - **BL-624** creates `Surl.Mcp.UnitLibrary` with `start_server`, `list_servers` and `stop_server`.
    Departures: `start_server` takes `arguments` (surl's words) and in-memory `files`/`files_base64`
    rather than typed options; with no root every path option is refused; `-K`/`--config` is refused; the
    path-member table and its reflection completeness test are added; at most 8 servers by default.
  - **BL-625** creates the `surl-mcp` executable. Departure: its command line (`--root`,
    `--allow-non-loopback`, `--max-servers`, `--log-level`, `--help`, `--version`) and `SURL_MCP_ROOT`
    are decided here and built there.
  - **BL-626** adds `get_exchanges`. Departure: no listen URL per exchange (ADR-0099 decision 7), so its
    Goal's and acceptance criteria's "listen URL" is dropped; the result is per-chunk in order with
    `after_exchange_id` and the 64 KiB and 1 MiB caps.
  - **BL-627** serves the `--aihelp` resources under `surl://aihelp`.
  - **BL-628** points `surl --aihelp`'s overview at `Surl.Mcp`.
  - **BL-632** drives the built `surl-mcp` with pinned upstream curl as the judge.
  - **BL-637** publishes the `surl-mcp` archives.
  - **BL-643** packs `Surl.Mcp`. Departure from the port: one framework-dependent tool package, not one
    native package per runtime.
  - **BL-649** documents `Surl.Mcp` once it exists.
  - **Amended to build on the core:** BL-463 (decisions 3 and 7 bound here), BL-464, BL-465, BL-467, and
    BL-468 (composition moved by BL-609 and BL-614).
- ADR-0002 decision 3's table gains `Surl.McpServing.UnitLibrary`, referencing nothing; BL-619 creates the
  project and its `ProtocolIsolationTests` row.
