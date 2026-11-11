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

None yet. On 2026-11-11 none of the extended track's `Integration` tasks is `Done`, so none has
recorded a port defect.

| Finding | Scheme | Integration task | Judged by |
| --- | --- | --- | --- |

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
| BL-456 | `socketio` |
| BL-462 | `webtransport` |
| BL-469 | `mcp` |
| BL-477 | `coap` |
| BL-486 | `dns` |
| BL-493 | `redis` |
| BL-502 | `mqtt5` |
| BL-508 | `amqp` |
| BL-515 | `amqp1` |
| BL-523 | `kafka` |
| BL-529 | `nats` |
| BL-534 | `stomp` |
| BL-541 | `xmpp` |
