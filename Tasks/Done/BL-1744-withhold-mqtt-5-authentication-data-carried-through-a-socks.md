---
id: BL-1744
title: Withhold MQTT 5 Authentication Data carried through a SOCKS tunnel from the exchange log
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1706, BL-1767]
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed: 2026-11-26
---
# BL-1744 — Withhold MQTT 5 Authentication Data carried through a SOCKS tunnel from the exchange log

## Goal

An MQTT 5 Authentication Data value carried through a SOCKS tunnel, in a `CONNECT` or an `AUTH` packet, is logged as
`****`.

## Context

- BL-1706 masks only the `CONNECT` password (ADR-0182, amendment 10, accepted limits).
  `Surl.LineProtocol`'s `TunnelMqttConnectRedactor`, which the SOCKS server and the HTTP proxy share since BL-1765
  (amendment 23), logs `CONNECT` properties and every later packet as received.
- The origin server's model is `Mqtt5ClientSecretBytesRedactor`. `Surl.LineProtocol` may not reference it (ADR-0002
  decision 3).
- Pinned upstream curl speaks MQTT 3.1.1 only, so the test bytes come from the MQTT 5.0 specification, sections 3.1 and
  3.15.

## Acceptance criteria

- [x] A named fast test shows an MQTT 5 `CONNECT` Authentication Data property logged as `****` through a granted
      tunnel, also when split across reads at every byte.
- [x] A named fast test shows the same for an `AUTH` packet sent after the `CONNECT`.
- [x] A named test shows the redactor never holds more than 64 KiB, and a later `PUBLISH` is logged as received.
- [x] ADR-0182 amendment 10's limits and the SOCKS `CLAUDE.md` say what is now masked.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the SOCKS library stays at 100% line and branch
      coverage.

## Notes

- Delivered in `Surl.LineProtocol`, so the SOCKS server and the HTTP proxy both mask it. Decision: ADR-0182 amendment
  28. No SOCKS production code changed.
- `TunnelMqttConnectRedactor` and `TunnelMqttConnectPasswordFinder` became `TunnelMqttLoginRedactor`,
  `TunnelMqttLoginBodyMasker`, `TunnelMqttPropertiesMasker` and `TunnelMqttFields`, because the redactor now masks more
  than the `CONNECT` password.
- Every tunnel packet is now framed; only `CONNECT` and `AUTH` are held. The protocol level is not consulted for
  `AUTH`, so a `CONNECT` too long to read cannot switch the masking off.
- `/feature` stages: the plan, tests and code were written in the lane session (21 new tests failed before the code),
  without separate architect, test-writer or implementer agents, to save tokens. `code-reviewer` reviewed it: no must
  fix. Its one should-fix (a protocol name length past the packet leaves a `CONNECT` unmasked) is amendment 10's
  accepted limit, which a non-conforming client alone reaches; left as it is and restated in amendment 28.
- Conformance: pinned upstream curl speaks MQTT 3.1.1 only; its recorded `CONNECT` tests still pass unchanged.
- `Application/Surl.Cli.UnitLibrary` still names MQTT 5 Authentication Data as logged: BL-1764, which depends on this
  task, rewrites it. `Surl.Protocol.HttpProxy`'s `CLAUDE.md` names only the password and LDAP bind (true, not complete);
  it is outside this task's `touches`.
- Verified 2026-11-26: `dotnet build -warnaserror` clean, `dotnet format --verify-no-changes` clean, 29941 fast tests
  passed, `Surl.LineProtocol` and `Surl.Protocol.Socks` at 100% line and branch coverage.

## Log

- 2026-11-25: Created.
- 2026-11-25: Now depends on BL-1767 (Stewart: 'prioritise 1767'): fix this in the shared tunnel line rules BL-1767 moves into Surl.LineProtocol, not in a per-server copy.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. MQTT 5 Authentication Data in a CONNECT or AUTH packet through a SOCKS or HTTP proxy tunnel is logged as ****
