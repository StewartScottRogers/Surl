# ADR-0116 — How the gRPC server answers `grpc` and `grpcs`

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-441. BL-442 builds the library with its message framing, protobuf codec, `grpc-timeout`
  parser, compression and status mapping (decisions 1, 4, 5 and 6), BL-443 answers calls on HTTP/2
  streams with validation, trailers, deadlines, authentication and limits (decisions 0 to 8),
  BL-444 answers server reflection (decision 9), BL-445 serves both schemes from `surl`
  (decision 0's listeners and ALPN), and BL-446 runs the Curl port's client against it.
- **Builds on:** [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  (the oracle is the specification; the Curl port is never proof; ports 80 and 443),
  [ADR-0086](ADR-0086-how-the-extended-servers-share-http-2-http-3-and-quic.md) decisions 1 and 6
  (`Surl.Http2` and its `IHttp2StreamHandler`; `grpc` prior knowledge, `grpcs` ALPN `h2`; what a
  `grpcs` server does without ALPN is this ADR's), [ADR-0081](ADR-0081-how-surl-serves-http-2.md)
  decision 5 (the HTTP/2 `SETTINGS` and limits `Surl.Http2` keeps),
  [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) sections 1, 3, 6
  and 8 (accounts, plain-text logins, `IHttpAuthenticationSession`, the refusal delay),
  [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decision 4 (one account set; a Bearer token is an empty-name account's password),
  [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) (limits and fixed error text) and
  [ADR-0010](ADR-0010-the-server-side-tls-contract.md) (TLS through the listener seam).
- **Amends:** nothing.

## Context

`Surl.Extended.Protocol.Grpc` (in `Protocols/Http/`, ADR-0083's table) answers the `grpc` and
`grpcs` schemes the Curl port adds, on ports 80 and 443 (ADR-0083 decision 7). By ADR-0083
decision 1 it is judged by its specification, not by upstream curl, which has no gRPC, and not by
the Curl port. The specifications read (2026-11-05, grpc/grpc `master`; the documents carry no
version, so the date read is the pin) are:

- **"gRPC over HTTP2"** (`doc/PROTOCOL-HTTP2.md`; below "PROTOCOL-HTTP2"): "Requests" (the
  Request-Headers grammar: Call-Definition, `te`, `grpc-timeout`, `content-type`,
  `grpc-encoding`, `grpc-accept-encoding`, Custom-Metadata; Length-Prefixed-Message), "Responses"
  (Response-Headers, Trailers, Trailers-Only, `grpc-status`, `grpc-message` percent-encoded),
  "HTTP2 Transport Mapping" ("Errors", "Connection Management": GOAWAY and PING);
- **"Status codes and their use in gRPC"** (`doc/statuscodes.md`; below "statuscodes"): the
  codes 0 to 16 and the table of codes the library generates;
- **"HTTP to gRPC Status Code Mapping"** (`doc/http-grpc-status-mapping.md`): for clients that get
  no `grpc-status`; it says servers should not use it to choose HTTP statuses;
- **"gRPC Compression"** (`doc/compression.md`; below "compression"): `UNIMPLEMENTED` with
  `grpc-accept-encoding` for an encoding the server lacks, `INTERNAL` for a set Compressed-Flag
  without an encoding;
- **"gRPC Server Reflection Tutorial"/design** (`doc/server-reflection.md`; below "reflection")
  with `src/proto/grpc/reflection/v1/reflection.proto` (below "reflection.proto");
- **the protobuf encoding guide** (<https://protobuf.dev/programming-guides/encoding/>) and
  `descriptor.proto` (`FileDescriptorProto` and the descriptors it holds).

Where these leave the server a choice - which services exist, the HTTP answer to a request that is
not gRPC, the status for a missing `te`, the limits - this ADR decides, and says so.

The port's `Curl.Extended.Protocol.Grpc.UnitLibrary` and its matrices `Grpc.md`,
`Grpc-Responses.md`, `Grpc-Transport.md` and `Grpc-Reflection.md` were read only to learn what a
client sends, so the server accepts it: one HTTP/2 connection (prior knowledge on `grpc`, TLS with
ALPN `h2` on `grpcs`, refusing the connection when `h2` is not agreed); Request-Headers in the
order `:method POST`, `:scheme`, `:path /<Service>/<Method>`, `:authority`, `grpc-timeout` when
`-m` is given, `te: trailers`, `content-type: application/grpc`, `user-agent`, then `-H` values as
Custom-Metadata (so `authorization` arrives as metadata); one uncompressed request message; no
`grpc-accept-encoding`; and, without a `.proto` file, a reflection v1 stream on the same connection
asking `file_containing_symbol` for the service, then `file_by_filename` for each missing
dependency, before the call. Nothing below is taken from the port's output, and no expected byte
comes from it.

## Decision

### 0. What the server is

- **One gRPC server** on HTTP/2. `Surl.Extended.Protocol.Grpc` references
  `Surl.Protocol.Abstractions` and `Surl.Http2` (ADR-0086 decision 1; BL-443 adds the reference),
  never another protocol server. It is an `IHttp2StreamHandler`: `Surl.Http2` owns the preface,
  `SETTINGS`, flow control, `PING` (echoed exactly, PROTOCOL-HTTP2 "PING"), `GOAWAY` and the
  HTTP/2 limits of ADR-0081 decision 5; the gRPC server sees one request stream per call. Time
  comes from `ExchangeContext.TimeProvider`. Protobuf is hand-written (no package, root
  `CLAUDE.md`).
- **`grpc`** is HTTP/2 with prior knowledge: the client's first bytes must be the HTTP/2 preface,
  answered as `Surl.Http2` answers an invalid one (RFC 9113 section 3.4: `GOAWAY`
  `PROTOCOL_ERROR`, then close). No `Upgrade: h2c` and no HTTP/1.1: PROTOCOL-HTTP2 maps gRPC onto
  HTTP/2 only.
- **`grpcs`** is TLS from the first byte (`ImplicitTlsSchemeServer`, ADR-0010), TLS 1.2 and 1.3,
  `--cert` or `--self-signed` required (`CertificateProblem` (58) without), no client certificate,
  ALPN offering exactly `h2` (BL-445). A client that offers no ALPN is still served when its first
  bytes are the HTTP/2 preface: ALPN is how a client learns the server speaks HTTP/2, and one that
  sends the preface already assumes it. A client whose ALPN list lacks `h2` gets whatever the
  platform's TLS gives (RFC 7301 section 3.2's `no_application_protocol` alert where it sends one),
  and if the handshake completes, the preface check above.
- **Ports** 80 (`grpc`) and 443 (`grpcs`) when the listen URL names none (ADR-0083 decision 7).

### 1. The services served

- **One built-in test service, `surl.test.Echo`**, in the file `surl/test/echo.proto`, plus
  reflection (decision 9). Nothing else: no health service (`grpc.health.v1.Health` is
  `UNIMPLEMENTED` like any unknown service), no services from a user's `.proto` file. A
  `.proto` given on the command line would need a way to produce replies that no gRPC document
  defines (a reply language, or canned replies per method); the built-in service already lets a
  client exercise each call shape, each status and deadlines, with or without reflection. A later
  ADR may add user services; nothing here prevents it.
- **The file**, served by reflection byte for byte as the `FileDescriptorProto` this text
  describes (BL-444 builds it in code, never with `protoc`), and printed by `surl --aihelp grpc`
  (BL-445) so a client can call with a `.proto` file instead of reflection:

  ```proto
  syntax = "proto3";

  package surl.test;

  message EchoRequest {
    string message = 1;
    uint32 count = 2;
    int32 status_code = 3;
    string status_message = 4;
    uint32 delay_milliseconds = 5;
  }

  message EchoReply {
    string message = 1;
    uint32 index = 2;
  }

  service Echo {
    rpc Echo (EchoRequest) returns (EchoReply);
    rpc Expand (EchoRequest) returns (stream EchoReply);
    rpc Collect (stream EchoRequest) returns (EchoReply);
    rpc Chat (stream EchoRequest) returns (stream EchoReply);
  }
  ```

- **What each method does.** `delay_milliseconds` (at most 60000) waits on the `TimeProvider`
  before each reply, so a test can make a deadline pass. `status_code` 0 means OK; 1 to 16 ends the
  call with that status and `status_message` as its `grpc-message`, so a test can see any status a
  server sends. Each is the service's own answer, not the library's (statuscodes lets an
  application send any code).
  - `Echo` (unary): one reply, `message` copied, `index` 0; or, with a `status_code`, no reply and
    Trailers-Only with that status.
  - `Expand` (server streaming): `count` replies (0 to 1000), each with `message` copied and
    `index` 0, 1, 2, …, then trailers with `status_code` (so a status may follow messages).
  - `Collect` (client streaming): reads every request until the client half-closes, then one reply
    whose `message` is the requests' messages concatenated in order and whose `index` is how many
    requests came; a request with a `status_code` ends the call with it at once, no reply.
  - `Chat` (bidirectional): one reply per request as it arrives (`message` copied, `index` its
    position from 0), each after its delay; a request with a `status_code` ends the call with it at
    once; trailers OK when the client half-closes.
  - `count` above 1000, `status_code` outside 0 to 16, or `delay_milliseconds` above 60000 is
    `INVALID_ARGUMENT` (3), `grpc-message` `request field out of range`.

### 2. Unary and streaming

All four call shapes are served, and reflection is bidirectional streaming (decision 9). Reading
and writing run independently on the `Http2RequestStream` (ADR-0086 decision 1), so `Chat` and
reflection answer each request before the client half-closes. A unary or server-streaming method
that receives no request message, or more than one, before the client half-closes is
`UNIMPLEMENTED` (12), `grpc-message` `request cardinality violation` (statuscodes, "Request
cardinality violation"). A request message whose protobuf does not decode (a truncated varint or
length, a known field with the wrong wire type, a string that is not UTF-8) is `INTERNAL` (13),
`grpc-message` `request message did not parse` (statuscodes, "Error parsing request proto").
Unknown fields are skipped, as the encoding guide says. An exception the service did not expect is
`UNKNOWN` (2), `grpc-message` `server error`, and the verbose log names it.

### 3. Request validation (PROTOCOL-HTTP2 "Requests")

`Surl.Http2` has already refused a malformed HTTP/2 request (RFC 9113 sections 8.2 and 8.3,
`RST_STREAM` `PROTOCOL_ERROR`). The gRPC server then checks, in this order, and answers the first
failure:

| Check | Answer | Why |
| --- | --- | --- |
| `:method` is `POST` | HTTP `405`, `allow: POST`, no body, `END_STREAM`; no `grpc-status` | not a gRPC call; PROTOCOL-HTTP2 names no answer, and the status mapping document says servers should not pick HTTP statuses from it, so the HTTP answer for a wrong method is given (RFC 9110 section 15.5.6); a client maps 405 to `UNKNOWN` |
| `content-type` begins with `application/grpc` | HTTP `415`, no body, `END_STREAM`; no `grpc-status` | PROTOCOL-HTTP2 "Requests": servers SHOULD answer 415, so other HTTP/2 clients do not read a gRPC error's 200 as success |
| `content-type` is `application/grpc` or `application/grpc+proto` (parameters after `;` ignored) | Trailers-Only `UNIMPLEMENTED` (12), `grpc-message` `content-type not served` | `+json` and custom subtypes are gRPC calls in a codec the server lacks; a gRPC status tells the client so |
| `te` is `trailers` | Trailers-Only `INTERNAL` (13), `grpc-message` `te: trailers required` | the grammar makes `te` part of every Call-Definition, "used to detect incompatible proxies"; a request without it came through something that may drop trailers, and the answer, being Trailers-Only, still arrives |
| authentication (decision 7) | `UNAUTHENTICATED` (16) or `PERMISSION_DENIED` (7) | decision 7 |
| `grpc-timeout` parses (decision 5) | Trailers-Only `INTERNAL` (13), `grpc-message` `malformed grpc-timeout` | the header is unreadable; statuscodes' parse failures are `INTERNAL` |
| `grpc-encoding` is served (decision 6) | Trailers-Only `UNIMPLEMENTED` (12) with `grpc-accept-encoding` | compression |
| `:path` is `/<service>/<method>` naming a served service and method | Trailers-Only `UNIMPLEMENTED` (12), `grpc-message` `unknown service` or `unknown method` | statuscodes, "Method not found at server"; judged after authentication, so an unauthenticated client learns nothing about what is served |

`:scheme`, `:authority`, `user-agent`, `grpc-message-type` and Custom-Metadata (ASCII and `-bin`)
are accepted and never checked or echoed; the verbose log records the metadata names. Request
headers are bounded by `Surl.Http2`'s `MAX_HEADER_LIST_SIZE` (`--max-message`), not
PROTOCOL-HTTP2's suggested 8 KiB: the shared library has one bound for every server.

### 4. Responses, Trailers-Only and `grpc-message` (PROTOCOL-HTTP2 "Responses")

- **A call that replies with messages** sends Response-Headers `:status 200`, `content-type` (the
  request's, `application/grpc` or `application/grpc+proto`) and
  `grpc-accept-encoding: identity,deflate,gzip`; then each reply as a Length-Prefixed-Message,
  Compressed-Flag 0; then Trailers (a `HEADERS` frame with `END_STREAM`) with `grpc-status` and,
  when not 0, `grpc-message`. `grpc-status` is sent even for OK ("Status must be sent in Trailers
  even if the status code is OK").
- **A call that ends before any message is sent** - every error above, and `Echo` or `Collect` with
  a `status_code` - is Trailers-Only: one `HEADERS` frame with `END_STREAM` holding `:status 200`,
  `content-type`, `grpc-status`, `grpc-message` and `grpc-accept-encoding`. Response-Headers are
  never sent without a message following, so an error is Trailers-Only exactly when nothing was
  written.
- **After the trailers**, if the client's half of the stream is still open, `RST_STREAM`
  `NO_ERROR` (RFC 9113 section 8.1), so a client still sending stops. `CANCEL` is sent only when a
  reply message was cut off mid-write (decision 5's deadline, the shutdown below): PROTOCOL-HTTP2
  "Errors" keeps it for an incomplete byte sequence.
- **`grpc-message`** is UTF-8, percent-encoded: every byte outside `%x20-%x7E`, and `%` itself, as
  `%` and two upper-case hex digits (PROTOCOL-HTTP2 "Responses", Percent-Encoded). The library's own
  texts are the fixed ASCII strings this ADR names; only `Echo`'s `status_message` comes from the
  client, and it is the client's own text.
- **A client's `RST_STREAM`** ends the call at once; nothing more is written, and the log records
  it as `CANCELLED`. **A connection that fails** cancels its calls (PROTOCOL-HTTP2 "Connection
  Management").
- **Shutdown, `--idle-timeout` and `--max-time`** are `Surl.Http2`'s `GOAWAY` naming the last stream
  processed, `NO_ERROR` (PROTOCOL-HTTP2: servers should send `GOAWAY` before terminating); calls in
  flight finish within the shutdown grace (ADR-0059), and any still open then get trailers
  `UNAVAILABLE` (14), `grpc-message` `server shutting down` (statuscodes, "Server shutting down"),
  or `RST_STREAM` `CANCEL` if a reply was cut off.
- The server sends no `PING` of its own: PROTOCOL-HTTP2 allows but does not require keep-alive
  PINGs, and `--idle-timeout` already ends a silent connection.

### 5. Deadlines (`grpc-timeout`)

- **Parsing** (PROTOCOL-HTTP2 "Requests", TimeoutValue and TimeoutUnit): 1 to 8 ASCII digits then
  exactly one unit `H`, `M`, `S`, `m`, `u` or `n`, nothing else, no spaces. Anything else -
  9 digits, no unit, two units, a sign - is malformed (decision 3). The grammar says "positive"; a
  value of 0 is read as a deadline already passed, the lenient reading of a clear intent, and gets
  `DEADLINE_EXCEEDED` at once. More than one `grpc-timeout` field is malformed.
- **No `grpc-timeout`** is an infinite deadline ("a server should assume an infinite timeout"),
  bounded only by `--max-time`.
- **The deadline** runs on the injected `TimeProvider` from when the request headers are judged.
  When it passes before the call ends, the call ends `DEADLINE_EXCEEDED` (4), `grpc-message`
  `deadline exceeded`: Trailers-Only if nothing was written, otherwise trailers after the last
  whole message, then decision 4's `RST_STREAM`. A deadline passing during a delay ends the delay.
  The deadline covers reflection streams too.

### 6. Compression (`grpc-encoding`, `grpc-accept-encoding`; compression)

- **Request encodings served:** `identity`, `gzip` (`GZipStream`) and `deflate` (the zlib format,
  `ZLibStream`), all in the BCL; names compared case-insensitively.
- **Another `grpc-encoding`** is Trailers-Only `UNIMPLEMENTED` (12), `grpc-message`
  `grpc-encoding not served`, with `grpc-accept-encoding: identity,deflate,gzip` (compression,
  "Compression Method Asymmetry Between Peers": the server MUST fail it `UNIMPLEMENTED` and list
  what it accepts, which never names the refused encoding). Checked before any message is read.
- **A message with Compressed-Flag 1** when `grpc-encoding` is absent or `identity` is `INTERNAL`
  (13), `grpc-message` `compressed flag set without grpc-encoding` (compression, test cases). A
  message that does not decompress is `INTERNAL`, `grpc-message` `message did not decompress`
  (statuscodes). Compressed-Flag 0 under any encoding is read as is.
- **Replies are never compressed**: Compressed-Flag 0, no `grpc-encoding` on the response. The
  client's `grpc-accept-encoding` is read and not needed; compression lets a peer reply
  uncompressed, and every reply's `grpc-accept-encoding` advertises what the server reads
  ("it MUST include said encoding in the response's `grpc-accept-encoding` header").

### 7. Authentication (ADR-0032; ADR-0100 decision 4)

- **Secure by default.** Every call, reflection included, is judged before its service is looked
  up, through the connection's `IHttpAuthenticationSession` (`StartHttpConnection`, ADR-0032 section
  6) with `HttpAuthenticationRequest("POST", <:path>, IsWrite: false, <regular fields>)`. The
  `authorization` metadata carries `Basic <base64(name:password)>` (an account) or
  `Bearer <token>` (an empty-name account whose password is the token, ADR-0100 decision 4), as
  the HTTP servers read them.
- **Only Basic and Bearer.** gRPC has no `401` round trip to carry a Digest, NTLM or Negotiate
  challenge, and the connection-bound ones cannot ride HTTP/2 (ADR-0081). An `authorization` with
  another scheme is `UNAUTHENTICATED` without asking the session.
- **Verdicts:** `Proceed` serves the call as the account named (or anonymous). `Challenge` is
  Trailers-Only `UNAUTHENTICATED` (16), `grpc-message` `unauthenticated`, carrying the verdict's
  `WWW-Authenticate` values as `www-authenticate` metadata (statuscodes: incorrect auth metadata
  is `UNAUTHENTICATED`; the status mapping document pairs it with 401). `Forbidden` is Trailers-Only
  `PERMISSION_DENIED` (7), `grpc-message` `permission denied`. Neither text names the account or
  the cause, and each waits the session's refusal delay (ADR-0032 section 8).
- **Plain text.** On `grpc` the session refuses credentials unless `--allow-plaintext-auth` is
  given (ADR-0032 section 3); `grpcs` logs in by default.
- **`--allow-anonymous`** serves calls without `authorization` as the anonymous caller; a call that
  carries credentials is still judged, and a bad one is never served as anonymous.
- Authorization is all or nothing: the built-in service touches no store, so there is no
  per-method permission and `PERMISSION_DENIED` comes only from the session.

### 8. Limits (ADR-0006)

| Limit | How the server applies it | Answer when hit |
| --- | --- | --- |
| a message (`--max-message`, 1048576) | the Length-Prefixed-Message length, judged from the 5-byte prefix before the message is read; and the decompressed size, decompression stopping one byte past it | `RESOURCE_EXHAUSTED` (8), `grpc-message` `message larger than the limit` (statuscodes, "larger than configured limit") |
| a call's request messages together (`--max-filesize`, 104857600) | the sum of their lengths on the wire | `RESOURCE_EXHAUSTED`, `grpc-message` `call larger than the limit` |
| a reply (`--max-message`) | `Collect`'s concatenation | `RESOURCE_EXHAUSTED`, `grpc-message` `reply larger than the limit` |
| concurrent calls | `Surl.Http2`'s `MAX_CONCURRENT_STREAMS` 100 a connection (ADR-0081 decision 5) | `RST_STREAM` `REFUSED_STREAM`, which a client maps to `UNAVAILABLE` (PROTOCOL-HTTP2 "Errors") |
| request headers, frames, flow control, head timeout | `Surl.Http2` (ADR-0081 decision 5) | its `GOAWAY` or `RST_STREAM` |
| `--max-connections`, `--max-connections-per-address` | `Surl.Http2`'s refusal (ADR-0081 decision 5) | `:status 503`, which a client maps to `UNAVAILABLE` |
| `--idle-timeout` (120 s), `--max-time` (3600 s) | `Surl.Http2` | `GOAWAY` `NO_ERROR`, decision 4 |
| `Expand`'s `count`, `delay_milliseconds` | 1000 replies, 60000 ms | `INVALID_ARGUMENT` (decision 1) |

No new option is added: ADR-0006's options cover what a test harness tunes.

### 9. Server reflection (reflection; reflection.proto)

- **Both versions.** `grpc.reflection.v1.ServerReflection/ServerReflectionInfo` and
  `grpc.reflection.v1alpha.ServerReflection/ServerReflectionInfo` are answered alike: the two
  `.proto` files declare the same messages and field numbers in different packages, and clients
  older than v1 ask v1alpha only. Each is described by its own file,
  `grpc/reflection/v1/reflection.proto` and `grpc/reflection/v1alpha/reflection.proto`.
- **One response per request**, in order, on the bidirectional stream, each written as soon as its
  request is read ("Any given request must either result in an error code or an answer"). Each
  response carries `valid_host` (the request's `host`) and `original_request` (the request as
  received). An `error_response` does not end the stream; the stream ends with trailers OK when
  the client half-closes.
- **`list_services`**: `list_services_response` naming `grpc.reflection.v1.ServerReflection`,
  `grpc.reflection.v1alpha.ServerReflection` and `surl.test.Echo`, in ordinal order.
- **`file_by_filename`**: the named file and its transitive dependencies ("all transitive
  dependencies of the returned file"), the requested file first. The three files import nothing,
  so each answer is one file.
- **`file_containing_symbol`**: the file declaring the fully qualified symbol, with its transitive
  dependencies. Symbols are packages' services (`surl.test.Echo`), methods (`surl.test.Echo.Echo`)
  and messages (`surl.test.EchoRequest`, `grpc.reflection.v1.ServerReflectionRequest`, …), and
  fields (`surl.test.EchoRequest.message`); a leading `.` is accepted.
- **No tracking of files sent.** reflection lets a server skip files already sent on the stream; the
  server always sends the whole closure, which is never wrong and keeps the stream stateless.
- **Extensions.** No file declares an extension: `file_containing_extension` is `error_response`
  `NOT_FOUND`; `all_extension_numbers_of_type` for a known message type is an
  `all_extension_numbers_response` with its `base_type_name` and no numbers, and for an unknown one
  `NOT_FOUND`.
- **Errors.** An unknown file name or symbol is `error_response` with `error_code` 5 (`NOT_FOUND`,
  reflection.proto's "Notably includes error code 5") and `error_message` `not found`; a request
  with no `message_request` set is `error_code` 3 (`INVALID_ARGUMENT`), `error_message`
  `no request kind`. These are the reflection service's answers, not library statuses, so
  statuscodes' "never generated by the library" does not apply.
- **Descriptors** are `FileDescriptorProto`s built in code with BL-442's codec: `name`, `package`,
  `message_type` (`DescriptorProto` with each `FieldDescriptorProto`'s `name`, `number`, `label`,
  `type`, `type_name` for messages, `json_name`), `service` (`ServiceDescriptorProto` with each
  `MethodDescriptorProto`'s `name`, `input_type`, `output_type`, and `client_streaming` and
  `server_streaming` each written only when true) and `syntax` `proto3`; reflection.proto's
  `oneof message_request` and `oneof message_response` as `oneof_decl` with each member's
  `oneof_index`. A field's `label` is `LABEL_REPEATED` for a `repeated` field (reflection.proto's
  `file_descriptor_proto`, `extension_number` and `service`) and `LABEL_OPTIONAL` for every other
  (`ProtoFieldDescriptor.IsRepeated`). A `ServerReflectionResponse` carries `valid_host` only when
  the request named a `host`.
- Reflection calls are judged by decisions 3, 5, 6, 7 and 8 like any call.

## Alternatives considered

- **Serve methods from a user's `.proto` file.** Rejected for now: replies would need a reply
  language no gRPC document defines, and a hand-written `.proto` parser; the built-in service
  covers each call shape and each status a client must handle. A later ADR may add it.
- **Accept a request without `te: trailers`.** Rejected: the grammar requires it, and refusing it
  with a status the client still receives tells it plainly that trailers may be lost on its path.
- **Answer a wrong `:method` with a gRPC status.** Rejected: such a request is not a gRPC call, and
  PROTOCOL-HTTP2 already answers the other non-gRPC case (wrong `content-type`) in HTTP.
- **Compress replies when the client accepts it.** Rejected: compression lets a server reply
  uncompressed, and one reply form is simpler to test; reading compressed requests is what a client
  needs.
- **Reflection v1 only.** Rejected: v1alpha costs a second descriptor and keeps older clients
  working.
- **PROTOCOL-HTTP2's suggested 8 KiB header bound.** Rejected: `Surl.Http2` has one
  `MAX_HEADER_LIST_SIZE` for every server, ADR-0081's `--max-message`.

## Consequences

- `surl grpc://127.0.0.1:0/` (with `--allow-anonymous`, or an account) is a gRPC server with the
  `surl.test.Echo` service in all four call shapes, deadlines, gzip and deflate requests, Basic and
  Bearer logins, and reflection v1 and v1alpha; `grpcs` adds TLS with ALPN `h2`.
- BL-442 to BL-444 build it, BL-445 serves it from `surl` (and puts the `.proto` text in
  `surl --aihelp grpc`), and the Curl port's client may connect to it as a live client in
  `Integration` tests (BL-446, ADR-0083).
- The requirement matrix [`Grpc.md`](../../Conformance/Extended/Grpc.md) traces each server-side
  requirement to BL-442, BL-443 or BL-444; each task replaces its ID with the named test.

## Amendment 1 - how a call open at shutdown is ended (BL-886, 2026-11-11)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-11.

Decision 4's shutdown bullet needs `Surl.Http2` to let a handler write once more after the
exchange is cancelled, because the exchange's cancellation (shutdown, `--idle-timeout`,
`--max-time`; ADR-0059) cuts every stream write off. The seam:

- **No change to `IHttp2StreamHandler` or `ExchangeContext`**, so `Surl.Protocol.Http`, the HTTPS
  proxy and DNS over HTTPS behave exactly as before; a handler that does nothing new is cut off
  as it always was.
- **`Http2RequestStream.IsExchangeCancelled`** says the connection's token is cancelled, which
  tells a handler's `OperationCanceledException` from the exchange apart from the client's
  `RST_STREAM` or close (which leave it false).
- **`Http2RequestStream.WriteFarewellTrailersAsync(fields)`** (a header block with `END_STREAM`)
  and **`WriteFarewellResetAsync(code)`** (`RST_STREAM`) are each written on a deadline token of
  their own, `Http2ConnectionServer.FarewellWriteDeadline` on the exchange's `TimeProvider` - the
  same bound as the connection's farewell `GOAWAY` (ADR-0059 decision 3). `ServeAsync` already
  awaits every handler before that `GOAWAY`, so a handler's farewell lands first.
- **gRPC** catches the cancellation `when (stream.IsExchangeCancelled)` before its client-`CANCELLED`
  catch and ends the call with `GrpcStatus.ServerShuttingDown` (`UNAVAILABLE`, `server shutting
  down`) as Trailers, or Trailers-Only when nothing was sent, or `RST_STREAM` `CANCEL` when a reply
  was cut off mid-write (`GrpcResponseWriter.IsWritingReply`). The log says `ended UNAVAILABLE`.

Rejected: a separate `ExchangeContext.ShutdownToken`-driven signal to every handler before the
writes are cut off. It would change when `Surl.Protocol.Http` and DNS over HTTPS stop writing,
and `--idle-timeout` and `--max-time` would need their own grace; ending the call after the
cut-off, on its own short deadline, needs neither.
