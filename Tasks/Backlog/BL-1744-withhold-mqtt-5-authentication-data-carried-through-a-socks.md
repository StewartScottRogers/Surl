---
id: BL-1744
title: Withhold MQTT 5 Authentication Data carried through a SOCKS tunnel from the exchange log
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1706]
touches: [Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1744 — Withhold MQTT 5 Authentication Data carried through a SOCKS tunnel from the exchange log

## Goal

An MQTT 5 Authentication Data value carried through a SOCKS tunnel, in a `CONNECT` or an `AUTH` packet, is logged as
`****`.

## Context

- BL-1706 masks only the `CONNECT` password (ADR-0182, amendment 10, accepted limits).
  `SocksTunnelMqttConnectRedactor` logs `CONNECT` properties and every later packet as received.
- The origin server's model is `Mqtt5ClientSecretBytesRedactor`. The SOCKS library may not reference it (ADR-0002
  decision 3).
- Pinned upstream curl speaks MQTT 3.1.1 only, so the test bytes come from the MQTT 5.0 specification, sections 3.1 and
  3.15.

## Acceptance criteria

- [ ] A named fast test shows an MQTT 5 `CONNECT` Authentication Data property logged as `****` through a granted
      tunnel, also when split across reads at every byte.
- [ ] A named fast test shows the same for an `AUTH` packet sent after the `CONNECT`.
- [ ] A named test shows the redactor never holds more than 64 KiB, and a later `PUBLISH` is logged as received.
- [ ] ADR-0182 amendment 10's limits and the SOCKS `CLAUDE.md` say what is now masked.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the SOCKS library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
