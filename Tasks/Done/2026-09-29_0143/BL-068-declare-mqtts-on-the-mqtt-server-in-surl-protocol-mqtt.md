---
id: BL-068
title: Declare mqtts on the MQTT server in Surl.Protocol.Mqtt
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-036, BL-065, BL-066]
touches: [Surl.Protocol.Mqtt.UnitLibrary, Surl.Protocol.Mqtt.UnitTests, Record-CurlExchange.ps1, Surl.Console, Surl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-068 — Declare mqtts on the MQTT server in Surl.Protocol.Mqtt

## Goal

`MqttProtocolServer.Schemes` is `mqtt` and `mqtts`, and pinned upstream curl 8.21.0
completes a subscribe and a publish over `mqtts://` against Surl's packets, proven by
recordings.

## Context

- ADR-0002: `mqtts` is the same MQTT server behind implicit TLS. ADR-0014, decision 6,
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

- [x] `MqttProtocolServer.Schemes` is `["mqtt", "mqtts"]` and its XML doc says so; the
      library's `CLAUDE.md` names both schemes.
- [x] Recordings exist for `curl -k mqtts://127.0.0.1:<P>/t` and
      `curl -k -d hi mqtts://127.0.0.1:<P>/t`, each exiting 0 against Surl's packets.
- [x] A fast test replays each recording's plaintext through `MqttProtocolServer` with an
      `ExchangeContext` whose listen URL scheme is `mqtts`, and asserts Surl's bytes equal
      the accepted ones.
- [x] `dotnet build Surl.Protocol.Mqtt.UnitLibrary -warnaserror` is clean, the fast tests
      are green with no `Integration` test in `Surl.Protocol.Mqtt.UnitTests`, and
      `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Mqtt.UnitLibrary`.

## Notes

Filed by BL-036, whose plan declared only `mqtt`.

- 2026-09-29 (dark factory lane 3): the recordings need `Record-CurlExchange.ps1 -Raw -Tls`,
  which the script still refuses (`-Raw serves plain TCP, so it cannot be combined with
  -Tls.`). BL-066, in Doing, adds exactly that combination and names
  `Record-CurlExchange.ps1` in its `touches`. Writing a throwaway TLS relay instead would
  break the rule to extend the recorder rather than serve curl from a private server, and
  extending the script here would collide with BL-066. So `Record-CurlExchange.ps1` joins
  this task's `touches` (in case BL-066's version still falls short for MQTT's binary
  replies), BL-066 joins `depends-on`, and the task goes back to Backlog until BL-066 is
  Done. No code was changed.
- 2026-09-29 (dark factory lane 2): BL-066's `-Raw -Tls` sufficed for MQTT's binary
  replies; `Record-CurlExchange.ps1` was not changed. Both recordings (`mqtts-subscribe-t`,
  `mqtts-publish-hi`) exited 0 with empty stderr, curl sending over TLS the same packets it
  sends over `mqtt`. Plan followed BL-066's gophers pattern, no new ADR needed: ADR-0002
  and ADR-0010 already decide that `mqtts` is the same server behind the engine's implicit
  handshake. Declaring `mqtts` adds it to `surl --version`'s `Protocols:` line, so
  `Surl.Console.UnitTests` (the `--version` test) and `Surl.Console` (its `CLAUDE.md`
  names each server's schemes) joined `touches`; no task in Doing names either
  (BL-061 touches Http, BL-073 Networking). Chose port 18884 for the recordings and tests
  (18883 is the plain `mqtt` one) and `-RawIdleMilliseconds 300`, as BL-066 did.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Waits on BL-066, which adds Record-CurlExchange.ps1 -Raw -Tls (touches Record-CurlExchange.ps1); the mqtts recordings need it.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. MqttProtocolServer answers mqtt and mqtts; pinned curl 8.21.0 subscribe and publish over mqtts recorded and replayed
