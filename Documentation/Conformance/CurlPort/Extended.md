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
| Date | 2026-11-11 (BL-592) |

## Findings

On 2026-11-11 four of the extended track's `Integration` tasks are `Done`: BL-456 (`socketio`), BL-462
(`webtransport`), BL-477 (`coap`) and BL-541 (`xmpp`). Only BL-462 recorded a port defect. BL-456 and
BL-541 recorded none. BL-477 recorded none either: the port's `coap` client exits 0 on a CoAP error
response, which BL-477 judged a client choice rather than a defect, and its refused-PUT test reads the
response code from `-i` output instead.

| Finding | Scheme | Integration task | Judged by |
| --- | --- | --- | --- |
| EXT-1, judged by the specification: the client exits 0 when its WebTransport CONNECT is refused `401` | `webtransport` | BL-462 | draft-ietf-webtrans-http3-16 section 3.2 (a non-2xx answer to the CONNECT means no session is established) |

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

## Extended tasks not yet done

Each of these runs the port's extended client against a live `surl`. When it is `Done`, a later run
of BL-592's summary, filed as its own task, adds the port defects its Notes record here and to the
handoff.

| Task | Client |
| --- | --- |
| BL-422 | `s3` |
| BL-428 | `az` and `abfs` |
| BL-434 | `gs` |
| BL-440 | `oci` |
| BL-446 | `grpc` |
| BL-451 | `signalr` |
| BL-469 | `mcp` |
| BL-486 | `dns` |
| BL-493 | `redis` |
| BL-502 | `mqtt5` |
| BL-508 | `amqp` |
| BL-515 | `amqp1` |
| BL-523 | `kafka` |
| BL-529 | `nats` |
| BL-534 | `stomp` |
