# ADR-0083 — How Surl serves the Curl port's extended protocols, and how they are tested

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Stewart, 2026-11-04, for the family of extended servers, their oracle, the
  permission to copy code, their project names and category folders, skipping Plugin, their
  isolation, one switch, unchanged original schemes and the same rules (the "Stewart's approval"
  list below). Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04, in
  BL-543, for every detail those leave open (decisions 1 to 10).
- **Amends:** [ADR-0003](ADR-0003-upstream-curl-is-surls-only-oracle.md) decision 1, for
  `Surl.Extended.Protocol.*` only (decision 1 below); the product overview's non-goal "Protocols
  upstream curl does not request" (decision 10). ADR-0002's isolation rule, ADR-0004's listener
  seam, ADR-0006's hardening and ADR-0079's category folders apply to the extended servers as
  written.

## Context

The Curl port (https://github.com/StewartScottRogers/Curl) adds URL schemes that upstream curl
refuses. Its `Documentation/Protocols.md`, section "Extended protocols", lists them; each has a
requirement matrix in its `Documentation/Conformance/Extended/<Name>.md` and client code in
`Curl.Extended.Protocol.<Name>.UnitLibrary`. As of 2026-11-04 there are 20: Amqp0, Amqp1,
AzureStorage, Coap, DnsQuery, GoogleCloudStorage, Grpc, Kafka, McpClient, Mqtt5, Nats,
OciRegistry, Plugin, Redis, S3, SignalR, SocketIo, Stomp, WebTransport and Xmpp.

Surl has so far served only what upstream curl asks for, and ADR-0003 makes upstream curl its only
oracle. Upstream curl cannot speak these schemes, so there is no upstream build to measure. The
port's own rules for its extensions (its ADR-0999 and ADR-1188) are background only, not evidence.

### Stewart's approval, 2026-11-04

These are Stewart's decisions and are recorded here, not reopened:

- **A server for each extended protocol.** Surl gets one server per extended protocol the Curl port
  adds, except Plugin.
- **Oracle.** Each extended server is judged by its protocol's specification. Expected bytes come
  from the specification and its examples, never from the Curl port. The port's extended client may
  be used as a live test client in integration tests, but its output is never proof. This is a
  narrow exception to ADR-0003; the original protocols keep pinned upstream curl as their only
  oracle.
- **Copying code.** Copying code from the port's client, and from its reference servers under
  `Curl.Conformance.Extended.UnitLibrary/<Name>/`, is allowed: code is not a verdict.
- **Layout.** Each server is `Surl.Extended.Protocol.<Name>.UnitLibrary` with its `.UnitTests`
  twin, in the ADR-0079 category folder for its job (table below). `Protocols/DataStore/` is a new
  category, which BL-400 adds by amending ADR-0079.
- **Plugin is skipped.**
- **Isolation.** An extended server references only `Surl.Protocol.Abstractions` and ADR-0002's
  horizontal libraries, never an original protocol server; no original server references an
  extended one.
- **One switch** turns every extended server off, mirroring the port's `CURL_EXTENSIONS=0`.
- **Unchanged original schemes.** An extended server never changes how an original scheme is
  served.
- **Same rules.** Secure by default (authentication loosened only by an option given explicitly),
  the four quality gates, the base class library only, and whatever the BCL lacks built by hand.

### The 20 servers

| Server | Schemes | Category folder | Port client it mates |
| --- | --- | --- | --- |
| `Surl.Extended.Protocol.S3` | `s3` | `Protocols/FileTransfer/` | S3 |
| `Surl.Extended.Protocol.AzureStorage` | `az`, `abfs`, `abfss` | `Protocols/FileTransfer/` | AzureStorage |
| `Surl.Extended.Protocol.GoogleCloudStorage` | `gs` | `Protocols/FileTransfer/` | GoogleCloudStorage |
| `Surl.Extended.Protocol.OciRegistry` | `oci` | `Protocols/FileTransfer/` | OciRegistry |
| `Surl.Extended.Protocol.Grpc` | `grpc`, `grpcs` | `Protocols/Http/` | Grpc |
| `Surl.Extended.Protocol.SignalR` | `signalr`, `signalrs` | `Protocols/Http/` | SignalR |
| `Surl.Extended.Protocol.SocketIo` | `socketio`, `socketios` | `Protocols/Http/` | SocketIo |
| `Surl.Extended.Protocol.WebTransport` | `webtransport` | `Protocols/Http/` | WebTransport |
| `Surl.Extended.Protocol.McpServer` | `mcp`, `mcps`, `mcp+stdio` | `Protocols/Http/` | McpClient |
| `Surl.Extended.Protocol.Coap` | `coap`, `coaps` | `Protocols/Http/` | Coap |
| `Surl.Extended.Protocol.DnsQuery` | `dns` | `Protocols/Lookup/` | DnsQuery |
| `Surl.Extended.Protocol.Mqtt5` | `mqtt5`, `mqtt5s`, `mqtt5+ws` | `Protocols/Messaging/` | Mqtt5 |
| `Surl.Extended.Protocol.Amqp0` | `amqp`, `amqps` | `Protocols/Messaging/` | Amqp0 |
| `Surl.Extended.Protocol.Amqp1` | `amqp1`, `amqp1s` | `Protocols/Messaging/` | Amqp1 |
| `Surl.Extended.Protocol.Kafka` | `kafka`, `kafkas` | `Protocols/Messaging/` | Kafka |
| `Surl.Extended.Protocol.Nats` | `nats`, `natss` | `Protocols/Messaging/` | Nats |
| `Surl.Extended.Protocol.Stomp` | `stomp`, `stomps` | `Protocols/Messaging/` | Stomp |
| `Surl.Extended.Protocol.Xmpp` | `xmpp`, `xmpps` | `Protocols/Messaging/` | Xmpp |
| `Surl.Extended.Protocol.Redis` | `redis`, `rediss` | `Protocols/DataStore/` | Redis |
| none (skipped, decision 5) | none fixed | none | Plugin |

That is 19 server libraries for the port's 20 protocols: Plugin has none (decision 5). Each
library's name in the table omits `.UnitLibrary`; its `.UnitTests` twin sits beside it.

## Decision

### 1. The oracle exception, worded narrowly

ADR-0003 decision 1 stands for every original protocol: pinned upstream curl is the only oracle, and
the Curl port is never a test client, a source of bytes or evidence. The exception covers only
projects named `Surl.Extended.Protocol.*` and the integration tests that run against them:

- **The specification is the oracle.** An extended server is right when it does what its protocol's
  specification (the document the port's `Protocols.md` row names, or its successor) says a server
  does. Each extended server's own ADR cites the specification sections it relies on.
- **Unit tests replay bytes written from the specification** and its published examples, through a
  hand-written fake `IConnection` (or `IDatagramFlow`), exactly as the original servers' tests do.
  No unit test's expected value, fixture or replayed request is copied from the port's output or
  its tests.
- **The port's client is a live client in `Integration` tests only.** It shows that one real client
  completes the exchange; it proves nothing about the bytes. Its output is never cited as proof in
  an ADR, never in a test name as the reason a value is right, and never used as an expected value.
  When the port's client and the specification disagree, the specification wins and the
  disagreement is filed on the port's own board, as ADR-0003 decision 6 already says for the
  original protocols.
- **Nothing else moves.** No original server's test may use the port, and no extended server's
  test may use the port as evidence for an original scheme.

### 2. How an integration test locates and pins the port's client

The port's client is pinned **by path and SHA-256**, as upstream curl is, in its own file
**`CurlPortBuilds.json`** at the repository root. It is never listed in `UpstreamCurlBuilds.json`,
and the two files never share a SHA-256 (a unit test checks this; BL-406).

- **Fields per entry:** `platform` (`win-x64`, `linux-x64`, `osx-arm64`), `path` (default location),
  `sha256`, `commit` (the port's `master` commit the binary was published from), `publishCommand`
  (the exact `dotnet publish` line used) and `published` (date). The `commit` and `publishCommand`
  make the binary reproducible; the SHA-256 is what is checked.
- **Who produces it and how:** a session (lane or interactive) clones or fetches the port, checks
  out the pinned commit, publishes its `curl` executable with the recorded command into a folder
  outside both repositories (default `%LOCALAPPDATA%\Surl\curl-port\<commit>\` on Windows,
  `~/.local/share/surl/curl-port/<commit>/` elsewhere), and records the result in
  `CurlPortBuilds.json`. Building Stewart's own repository is not an upstream curl download and
  needs no approval; re-pinning is an ordinary commit to `CurlPortBuilds.json`, not an ADR, because
  the port is not an oracle.
- **Refusal:** the port's own locator (`CurlPortClientLocator`, planning name) hashes the file and
  refuses a binary whose SHA-256 is not pinned with its own exception (`UnpinnedCurlPortClientException`,
  planning name). A missing binary marks the test Inconclusive, as `UpstreamCurlUnavailability`
  does for upstream curl. The port's locator never returns a path listed in
  `UpstreamCurlBuilds.json`, and the upstream locator never accepts the port: they are separate
  types so neither can be handed the other's binary.
- **CI does not run them.** They are `[TestCategory("Integration")]`, which the CI fast run
  excludes, and no CI runner has the port's binary; they run on a developer's or a lane's machine
  when a pinned binary is present. Surl's correctness never depends on them, because the unit tests
  replaying specification bytes are the proof.

### 3. Where the extended integration tests live

In `Conformance/Surl.Conformance.UnitTests`, one test class per server, named
`CurlPortClient<Verb>SurlOver<Name>Tests` (for example `CurlPortClientPublishesToSurlOverMqtt5Tests`),
beside the existing `UpstreamCurl…SurlOver…Tests` classes so the two families sort apart. The
locator, pins and runner live in `Conformance/Surl.Conformance.UnitLibrary` (BL-406 builds them).
Each server's integration task adds its class.

### 4. Copied code

Copying code from the port's client libraries and from its reference servers under
`Curl.Conformance.Extended.UnitLibrary/<Name>/` is allowed. Once copied, a file is Surl's: it is
renamed to Surl's names and glossary, it meets the four quality gates, it uses the BCL only, and
its tests are written from the specification. A copied file's tests are not copied along with it,
because a copied test would carry the port's verdict.

### 5. Plugin is skipped

The port's Plugin protocol hands a URL whose scheme nothing else serves to a local helper program
(`curl-proto-<scheme>` on `PATH`, modelled on git's remote helpers). It has no wire protocol and no
fixed scheme, so there is nothing for a server to answer: a Plugin URL reaches whatever server the
helper talks to. No `Surl.Extended.Protocol.Plugin` is built.

### 6. The switch

- **Name and values:** the environment variable **`SURL_EXTENSIONS`**. The value `0` turns every
  extended server off; unset, empty or any other value leaves them on. This mirrors the port's
  `CURL_EXTENSIONS=0`, so one habit serves both.
- **Where it is read:** in `Surl.Console`, once, at composition, through an injected environment
  reader (planning name `IEnvironmentVariableReader`, with a test fake), never
  `Environment.GetEnvironmentVariable` in a static. With the switch off the extended servers are
  not composed and their schemes are not added to the listen-URL parser.
- **An extended scheme while the switch is off** is refused exactly as an unknown scheme is today:
  `ListenUrlParser` answers `SurlExitCode.UnsupportedProtocol` (1) with
  `surl: (1) Protocol "<scheme>" not supported`. A user who sets the switch has said they want
  original Surl, and original Surl does not know the scheme.
- **No command-line option** duplicates the switch; one switch, as in the port.

### 7. Default ports for extended schemes

`Application/Surl.Cli.UnitLibrary/SchemeDefaultPorts.cs` stays "upstream curl 8.21.0's default port
per scheme" and gains nothing. The extended schemes get their own table (planning name
`ExtendedSchemeDefaultPorts`, BL-405), which `ListenUrlParser` consults only while extensions are
on. No extended scheme shadows an original one (none does: `mqtt5` is not `mqtt`). Each port is
taken from the protocol's specification or IANA's Service Name and Transport Protocol Port Number
Registry; where neither fixes one, the port the protocol's own documentation uses, so marked:

| Scheme | Port | Source |
| --- | --- | --- |
| `s3`, `az`, `abfss`, `gs`, `oci` | 443 | HTTPS (RFC 9110 section 4.2.2); each service is served over HTTPS |
| `abfs` | 80 | HTTP (RFC 9110 section 4.2.1); `abfs` is the Data Lake scheme without TLS |
| `grpc`, `signalr`, `socketio`, `mcp` | 80 | HTTP (RFC 9110 section 4.2.1); each rides HTTP without TLS |
| `grpcs`, `signalrs`, `socketios`, `mcps` | 443 | HTTPS (RFC 9110 section 4.2.2) |
| `webtransport` | 443 | HTTPS over HTTP/3 (draft-ietf-webtrans-http3, RFC 9114) |
| `mcp+stdio` | none | Served on `surl`'s own standard input and output; the listen URL names no port (the McpServer ADR decides its form) |
| `coap` | 5683 | RFC 7252 section 6.1, IANA `coap` |
| `coaps` | 5684 | RFC 7252 section 6.2, IANA `coaps` |
| `dns` | 53 | RFC 1035 section 4.2, IANA `domain` |
| `mqtt5` | 1883 | MQTT 5.0 section 4.2, IANA `mqtt` |
| `mqtt5s` | 8883 | MQTT 5.0 section 4.2, IANA `secure-mqtt` |
| `mqtt5+ws` | 80 | WebSocket (RFC 6455 section 3), MQTT 5.0 section 6 |
| `amqp`, `amqp1` | 5672 | AMQP 0-9-1 section 4.2.2 and AMQP 1.0 part 2 section 2.1, IANA `amqp` |
| `amqps`, `amqp1s` | 5671 | IANA `amqps` |
| `kafka` | 9092 | Apache Kafka documentation (default `listeners`); not IANA-registered |
| `kafkas` | 9093 | Apache Kafka documentation's TLS listener examples; not IANA-registered |
| `nats`, `natss` | 4222 | NATS documentation (client port; TLS on the same port); not IANA-registered |
| `stomp` | 61613 | The port the STOMP brokers' documentation uses (ActiveMQ, RabbitMQ); STOMP 1.2 fixes none |
| `stomps` | 61614 | As `stomp`, their TLS port |
| `xmpp` | 5222 | RFC 6120 section 14.7, IANA `xmpp-client` |
| `xmpps` | 5223 | XEP-0368 (direct TLS), the conventional direct-TLS client port |
| `redis`, `rediss` | 6379 | IANA `redis`; Redis serves TLS on a port of its own choosing and documents 6379 |

The table's source column is carried into the code's doc comment, row for row.

### 8. The `--aihelp` shape and `--version`

- **Topics:** one `extended` topic and one topic per extended server, named after its first scheme
  family as the original servers' topics are (`s3`, `azure-storage`, …; each server's ADR names
  its topic). The `extended` topic's about text says these servers answer schemes upstream curl
  does not have, that each is judged by its protocol's specification and **not validated against
  upstream curl**, that `SURL_EXTENSIONS=0` turns them all off, and lists the extended topics. Each
  server's own topic repeats the one-line "judged by its specification, not by upstream curl"
  sentence. BL-405 adds the `extended` topic; each server's task adds its own.
- **`--version`:** the `Protocols:` line stays upstream curl's list of original schemes, unchanged.
  While extensions are on and at least one extended server is registered, one further line,
  `Extended: <scheme> <scheme> …` (ordinal order), follows it; with the switch off, or none
  registered, the line is absent. This keeps a script that parses `Protocols:` unaffected, as the
  port keeps its own extensions out of curl's line.

### 9. Requirement traceability

One server-side requirement matrix per protocol in `Documentation/Conformance/Extended/<Name>.md`,
with columns **ID**, **Level** (MUST, SHOULD, MAY), **Requirement** and **Covered by**, holding
only the requirements that bind a server. Each server's ADR task creates its matrix from the
specification (never from the port's client matrix); its integration task fills in the last
"Covered by" entries. `Documentation/Product/Requirements.md` holds one functional requirement for
the family (FR-056), and each server's ADR task adds its own row after it.

### 10. Roadmap placement

A **parallel track, "Extended servers"**, not a phase after Phase 7. It starts now and waits only
on the pieces it needs: the shared groundwork (BL-400 to BL-407), and, per server, the Phase 6
pieces it rides - gRPC on HTTP/2 (BL-365 to BL-371), WebTransport on HTTP/3 and QUIC (BL-373 to
BL-385), `coaps` on the DTLS decision (BL-403). Phase 7 then measures the port's extended clients
against these servers beside its original ones. The product overview's non-goal "Protocols upstream
curl does not request" is narrowed to "except the extended servers of ADR-0083".

## Consequences

Good:

- The port's extensions get a mate built the same way as everything else, judged by an instrument
  - the specification - that the port did not write.
- ADR-0003's circle stays closed for every original protocol; the exception is fenced by project
  name and test category.
- One switch returns `surl` to the original product.

Costs and caveats:

- For an extended protocol there is no independent client oracle; a misread specification is
  caught only by review and by the live port client disagreeing. Accepted: the specification is
  what both sides are meant to obey, and a disagreement is a finding either way.
- The port's binary is built locally and pinned per platform; until a session pins one, the
  integration tests are Inconclusive.
- Several default ports are conventions, not registrations (Kafka, NATS, STOMP, `xmpps`); they are
  marked so and change only by amending this ADR.

## Alternatives considered

- **Use the port's client as the oracle.** Rejected by Stewart: it would bake the port's defects
  into Surl, the circle ADR-0003 exists to prevent.
- **Pin the port by commit only and build it inside each test run.** Rejected: a test that builds
  another solution is slow and depends on that solution's SDK; the SHA-256 pin is what proves the
  binary is the one recorded, and the commit field still makes it reproducible.
- **List the port in `UpstreamCurlBuilds.json`.** Rejected: one locator could then hand the port to
  an upstream-curl test.
- **Add the extended schemes to `SchemeDefaultPorts`.** Rejected: that table is upstream curl's, and
  the switch has to remove them.
- **A `--no-extensions` option instead of the variable.** Rejected: the port has one switch, the
  variable; one habit for both programs.
