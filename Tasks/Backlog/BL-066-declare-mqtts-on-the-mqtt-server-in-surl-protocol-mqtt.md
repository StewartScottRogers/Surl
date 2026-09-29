---
id: BL-066
title: Declare mqtts on the MQTT server in Surl.Protocol.Mqtt
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-036, BL-065]
touches: [Surl.Protocol.Mqtt.UnitLibrary, Surl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-066 — Declare mqtts on the MQTT server in Surl.Protocol.Mqtt

## Goal

`MqttProtocolServer.Schemes` is `mqtt` and `mqtts`, and pinned upstream curl 8.21.0
completes a subscribe and a publish over `mqtts://` against Surl's packets, proven by
recordings.

## Context

- ADR-0002: `mqtts` is the same MQTT server behind implicit TLS. ADR-0012, decision 6,
  leaves declaring it to this task; ADR-0010 is the server-side TLS contract, and BL-065
  makes the serving engine perform the implicit handshake, so the server itself sees
  plaintext on `mqtts` exactly as on `mqtt`.
- Measure with `Record-CurlExchange.ps1 -Raw -Tls` (extend the script if `-Raw` and
  `-Tls` cannot yet be combined - BL-029 refuses the combination) against the pinned
  build with `-k`, feeding the packets of BL-036's `subscribe-t` and `publish-hi`
  fixtures. Record curl's exit code and stderr as the build reports them.
- Fixtures go under `Surl.Protocol.Mqtt.UnitTests/Fixtures/<case>/`, embedded, with the
  command lines added to its `README.md`.

## Acceptance criteria

- [ ] `MqttProtocolServer.Schemes` is `["mqtt", "mqtts"]` and its XML doc says so; the
      library's `CLAUDE.md` names both schemes.
- [ ] Recordings exist for `curl -k mqtts://127.0.0.1:<P>/t` and
      `curl -k -d hi mqtts://127.0.0.1:<P>/t`, each exiting 0 against Surl's packets.
- [ ] A fast test replays each recording's plaintext through `MqttProtocolServer` with an
      `ExchangeContext` whose listen URL scheme is `mqtts`, and asserts Surl's bytes equal
      the accepted ones.
- [ ] `dotnet build Surl.Protocol.Mqtt.UnitLibrary -warnaserror` is clean, the fast tests
      are green with no `Integration` test in `Surl.Protocol.Mqtt.UnitTests`, and
      `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Mqtt.UnitLibrary`.

## Notes

Filed by BL-036, whose plan declared only `mqtt`.

## Log

- 2026-09-28: Created.
