# Phase 7 findings: the Curl port's extended clients

The Curl port adds schemes upstream curl does not have, and Surl answers them with its extended servers,
`Surl.Extended.Protocol.*` (ADR-0083). Upstream curl has no counterpart to run beside an extended client,
so Phase 7 does not run them again: the port's extended clients meet Surl's extended servers in the
extended track's own `Integration` tasks, each **judged by the specification** of its protocol, and a
port defect one of them finds is recorded in that task's Notes
([ADR-0089](../../Planning/Decisions/ADR-0089-how-phase-7-measures-the-curl-port-against-surl-beside-pinned-upstream-curl.md)
decision 9). This file writes each such defect as a Phase 7 finding in decision 6's form, marked
"judged by the specification", and [`Port-Board-Handoff.md`](Port-Board-Handoff.md) carries it to the
port's board beside the original families' findings.

ADR-0083's oracle rule stands: a finding here is about the port, never evidence about Surl, and the
port's output is never an expected value.

## Measured

| What | Pin |
| --- | --- |
| Curl port | commit `b42c319ba940f66452b2b6f3e9910d46825b08fe`, `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7` (`CurlPortBuilds.json`, `win-x64`), the pin the extended `Integration` tests use (ADR-0089 decision 10) |
| Upstream curl | none: no upstream build has these schemes |
| Judged by | each protocol's specification, as each `Integration` task names it |
| Date | 2026-11-11 (BL-592); refreshed 2026-11-14 (BL-1124) |

## Findings

On 2026-11-14 every extended `Integration` task is `Done`: 19 clients. Their Notes record six port
defects, EXT-1 to EXT-6. Two port behaviours are choices, not defects, and are not findings: BL-477
(`coap` exits 0 on a CoAP error response; its refused-PUT test reads the code from `-i` output) and
BL-469 (`mcp` exits 8 on a `401`; the tests assert only non-zero).

| Finding | Scheme | Integration task | Judged by |
| --- | --- | --- | --- |
| EXT-1, judged by the specification: the client exits 0 when its WebTransport CONNECT is refused `401` | `webtransport` | BL-462 | draft-ietf-webtrans-http3-16 section 3.2 (a non-2xx answer to the CONNECT means no session is established) |
| EXT-2, judged by the specification: the client ignores `-u`, so a login is sent as `-H "Authorization: Basic ..."` | `grpc` | BL-446 | curl's `-u` contract (user and password for server authentication) |
| EXT-3, judged by the specification: the client exits 0 with no output when negotiate is answered `401` | `signalr` | BL-451 | ASP.NET Core SignalR transport protocol (a refused negotiate means no connection) |
| EXT-4, judged by the specification: the client exits 8 for MQTT 5 CONNACK `0x86` where the port's own ADR-1251 maps it to 67 | `mqtt5` | BL-502 | MQTT 5.0 section 3.2.2.2 (0x86, Bad User Name or Password); the port's ADR-1251 |
| EXT-5, judged by the specification: `-T file` to `amqp://` exits 0 yet nothing reaches the queue (`-d` publishes) | `amqp` | BL-508 | AMQP 0-9-1 `basic.publish` (the requested transfer must happen or fail) |
| EXT-6, judged by the specification: the Metadata v4 request omits `allow_auto_topic_creation`, so a produce to an undeclared topic fails `UNKNOWN_TOPIC_OR_PARTITION` (exit 8) | `kafka` | BL-523 | Kafka Metadata v4 (the flag is true when absent from earlier versions); Surl ADR-0105 decision 2 |

No port defect recorded: BL-422 (`s3`), BL-428 (`az` and `abfss`; plain-HTTP `abfs` is not reachable by
the port's client), BL-434 (`gs`), BL-440 (`oci`), BL-456 (`socketio`), BL-469 (`mcp`), BL-477 (`coap`),
BL-486 (`dns`), BL-493 (`redis`), BL-515 (`amqp1`), BL-529 (`nats`), BL-534 (`stomp`), BL-541 (`xmpp`).
EXT-2 to EXT-6 are summarised from each task's Notes and are reported, not asserted: the tests read the
port's output only to see what the server was required to send.

### EXT-1. The port's webtransport client exits 0 on a refused CONNECT

- **Case:** `PinnedCurlPortClientTalksToSurlOverWebTransportTests.Connect_WithoutCredentials_EchoesNothing`
  (`Surl.Conformance.UnitTests`, `Integration`), Surl commit `477dda7d5e31ed2a906b2505853800d227260dc9`;
  row `WebTransport:Surl-7-1` of [`../Extended/WebTransport.md`](../Extended/WebTransport.md).
- **Server:** `surl webtransport://127.0.0.1:<port>/ --self-signed --user <name>:<password>`.
- **Command line:** `curl -k -d "hello refused" webtransport://127.0.0.1:<port>/`, with no credentials.
- **Surl's answer:** `:status 401` with its challenges and FIN on the CONNECT stream, as the unit tests
  of row `WebTransport:Surl-7-1` prove.
- **Port result:** exit code 0, empty stdout; no session, nothing echoed.
- **What decides:** draft-ietf-webtrans-http3-16 section 3.2: a session is established only by a 2xx
  answer to the extended CONNECT, so a `401` means the transfer the command line asked for did not
  happen. Upstream curl's own nearest case agrees: a refused WebSocket upgrade ends the transfer with
  an error, not exit 0 (curl 8.21.0 `lib/ws.c`, "Refused WebSockets upgrade";
  https://curl.se/libcurl/c/libcurl-errors.html). The exact exit code is the port's to choose and record.
