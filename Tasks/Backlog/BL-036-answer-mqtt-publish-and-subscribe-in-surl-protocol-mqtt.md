---
id: BL-036
title: Answer MQTT publish and subscribe in Surl.Protocol.Mqtt
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-005, BL-029]
touches: [Surl.Protocol.Mqtt.UnitLibrary, Surl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-09-28
completed:
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

- [ ] Recordings exist for a subscribe (`curl mqtt://127.0.0.1:<P>/t`) and a publish
      (`curl -d hi mqtt://127.0.0.1:<P>/t`). Each is fed Surl's intended packets, and
      the pinned build exits 0, with stdout holding the delivered payload for the
      subscribe.
- [ ] Fast tests replay each recording and assert Surl's packets equal the accepted ones,
      including the remaining-length encoding (MQTT 3.1.1 section 2.2.3) for a payload of
      128 bytes or more.
- [ ] Fast tests cover a malformed remaining length, a packet over the size limit, an
      unsupported protocol level, and a connection closed mid-packet.
- [ ] `ProtocolIsolationTests` pass. `dotnet build Surl.Protocol.Mqtt.UnitLibrary
      -warnaserror` is clean, the fast tests are green with no `Integration` test in
      `Surl.Protocol.Mqtt.UnitTests`, and `Measure-CodeQuality.ps1` reports no failing
      member in `Surl.Protocol.Mqtt.UnitLibrary`.

## Notes

Wiring `mqtt` into `surl` and the live conformance run are BL-042.

## Log

- 2026-09-28: Created.
