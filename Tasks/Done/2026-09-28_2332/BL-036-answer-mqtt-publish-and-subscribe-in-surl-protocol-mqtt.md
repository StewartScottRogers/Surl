---
id: BL-036
title: Answer MQTT publish and subscribe in Surl.Protocol.Mqtt
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-005, BL-029]
touches: [Surl.Protocol.Mqtt.UnitLibrary, Surl.Protocol.Mqtt.UnitTests, Documentation/Planning/Decisions/ADR-0014-how-the-mqtt-server-answers.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-036 — Answer MQTT publish and subscribe in Surl.Protocol.Mqtt

## Goal

`Surl.Protocol.Mqtt.UnitLibrary` contains an MQTT protocol server for the `mqtt` scheme.
It completes the CONNECT, SUBSCRIBE and PUBLISH exchanges the pinned upstream curl 8.21.0
build performs, proven by byte scripts recorded from that build.

## Context

- MQTT 3.1.1 (OASIS standard) defines the packets. Which packets upstream curl sends for
  `curl mqtt://host/topic` (fetch) and for `curl -d payload mqtt://host/topic`
  (publish), with which protocol level, flags, client identifier and QoS, and what it
  expects back, is measured with `Record-CurlExchange.ps1 -Raw` (BL-029) against the
  pinned build, never assumed.
- `mqtts` is the same server over TLS (ADR-0002). This task declares only `mqtt`, and
  the plan notes the `mqtts` follow-up.
- Decide in the `/feature` plan, and state in the server's XML doc: what a subscriber
  receives (for example the last message published to the topic, retained per topic, or
  a configured payload), and how CONNECT with an unsupported protocol level is refused
  (CONNACK return code, MQTT 3.1.1 section 3.2.2.3).
- Packet-size limit: follow the hardening ADR (BL-024) if it exists when this task
  runs. If not, choose one, document it, and file a follow-up.
- The server implements the protocol-server interface from the listener-seam ADR
  (BL-000, BL-005), and is tested through BL-005's in-memory connection.
- Fixtures: as in BL-017, under `Surl.Protocol.Mqtt.UnitTests/Fixtures/<case>/`,
  embedded, with a `README.md`.

## Acceptance criteria

- [x] Recordings exist for a subscribe (`curl mqtt://127.0.0.1:<P>/t`) and a publish
      (`curl -d hi mqtt://127.0.0.1:<P>/t`). Each is fed Surl's intended packets, and
      the pinned build exits 0, with stdout holding the delivered payload for the
      subscribe.
- [x] Fast tests replay each recording and assert Surl's packets equal the accepted ones,
      including the remaining-length encoding (MQTT 3.1.1 section 2.2.3) for a payload of
      128 bytes or more.
- [x] Fast tests cover a malformed remaining length, a packet over the size limit, an
      unsupported protocol level, and a connection closed mid-packet.
- [x] `ProtocolIsolationTests` pass. `dotnet build Surl.Protocol.Mqtt.UnitLibrary
      -warnaserror` is clean, the fast tests are green with no `Integration` test in
      `Surl.Protocol.Mqtt.UnitTests`, and `Measure-CodeQuality.ps1` reports no failing
      member in `Surl.Protocol.Mqtt.UnitLibrary`.

## Notes

Wiring `mqtt` into `surl` and the live conformance run are BL-042.

**Plan and what was learned (2026-09-28).** Measured first with `Record-CurlExchange.ps1
-Raw` against the pinned build: curl sends `CONNECT` level 4, CleanSession, keep-alive 60,
client id `curl` + 8 random characters; a fetch then sends `SUBSCRIBE` (id 1, QoS 0) and
prints every `PUBLISH` it gets as topic length + topic + payload, and exits 0 only when
the server sends `DISCONNECT` (a bare close is exit 56); a publish sends QoS 0 `PUBLISH`
with RETAIN clear, then `DISCONNECT`, needing only `CONNACK`. `CONNACK` 1 makes curl exit
8. Decisions (ADR-0014, decided by Claude under Stewart's delegation): every publish is
kept as its topic's retained message whatever its RETAIN flag; a subscribe is one-shot -
`SUBACK` (QoS 0 granted, `0x80` for an invalid filter), each matching retained message as
a `PUBLISH` with RETAIN set, then `DISCONNECT`; an unsupported level gets `CONNACK` 1 and a
close; violations and limits close with no bytes (ADR-0006 section 5). QoS 1/2, `PUBREL`,
`UNSUBSCRIBE` and `PINGREQ` are answered too although curl sends none of them.

**Packet-size limit.** ADR-0006 (BL-024) exists, so `ExchangeLimits.MaxMessageBytes`
(1 MiB) counts the whole packet and is decided from the fixed header before any body byte
is read. The first-packet head timeout, the `PUBLISH` payload limit and curl's recorded
reaction to each close stay with BL-053, which already exists; no new follow-up needed.

**Choices with a sensible default.** Fixtures carry a sixth file, `reply.bin` (the exact
bytes fed to curl), because an MQTT server's reply is not in curl's stdout the way a DICT
reply is. The server takes an `MqttRetainedMessages` in its constructor so BL-042's
composition shares one store across connections. Only `mqtt` is declared; `mqtts` is
BL-068 (filed, depends on BL-065's implicit handshake).

**Review.** `code-reviewer` found six defects, all fixed before Done: the shared retained
store was unbounded (now 10000 topics / 100 MiB, ADR-0014 decision 7, a publish past it
closes with no reply); with `MaxMessageBytes` 0 the reader allocated the announced
remaining length up front (the buffer now grows as bytes arrive); each subscribe re-split
every filter per topic (filters are now de-duplicated and split once); section 3.1.2's
connect-flag rules, DUP on QoS 0, `PUBREL` id 0 and an empty `UNSUBSCRIBE` filter were
accepted (now closed); reserved types 0 and 15 were logged as server-only packets (now
logged as reserved).

**Touches widened.** Added `Documentation/Planning/Decisions/ADR-0014-how-the-mqtt-server-answers.md`
and the Decisions `README.md` index, for the ADR the unattended rules require; no task in
Doing named either. Another lane writing an ADR in the same shift may also take number
0012; the shift's merge renumbers as it did for ADR-0010.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. MqttProtocolServer answers pinned upstream curl 8.21.0's CONNECT, SUBSCRIBE and PUBLISH, proven by six recorded exchanges replayed in fast tests
