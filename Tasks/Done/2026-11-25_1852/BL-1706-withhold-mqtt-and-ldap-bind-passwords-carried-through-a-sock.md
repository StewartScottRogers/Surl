---
id: BL-1706
title: Withhold MQTT and LDAP bind passwords carried through a SOCKS tunnel from the exchange log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1710]
touches: [Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1706 — Withhold MQTT and LDAP bind passwords carried through a SOCKS tunnel from the exchange log

## Goal

An MQTT 3.1.1 or 5 `CONNECT` password and an LDAP simple bind password carried through a SOCKS tunnel are logged as
`****`.

## Context

- BL-1697 logs a tunnel whose first byte is not an ASCII letter byte for byte (ADR-0182 Amendment 6, accepted limits).
  MQTT `CONNECT` starts `0x10` and an LDAP message `0x30`.
- The origin servers' redactors are the model: `MqttConnectBytesRedactor`, `Mqtt5ClientSecretBytesRedactor`,
  `LdapBindBytesRedactor` (ADR-0182 Amendment 5). The SOCKS library may reference only what ADR-0002 decision 3's
  table allows, so it may need small framing parsers of its own.
- TLS (`0x16`) and every other binary tunnel must still pass through unchanged and unheld.

## Acceptance criteria

- [x] Named fast tests show an MQTT 3.1.1 and an MQTT 5 `CONNECT` password logged as `****` through a granted tunnel,
      also when split across reads at every byte.
- [x] A named test shows the same for an LDAP simple bind.
- [x] Named tests show a tunnel starting `0x16`, and a malformed `0x10` and `0x30` tunnel, each handled without the
      redactor holding more than 64 KiB.
- [x] ADR-0182 Amendment 6's limits and the SOCKS `CLAUDE.md` say what is now masked.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the SOCKS library stays at 100% line and branch
      coverage.

## Notes

- Design, recorded in ADR-0182 amendment 10 (decided by Claude under Stewart's delegation): the tunnel's first byte
  picks `SocksTunnelMqttConnectRedactor` (`0x10`) or `SocksTunnelLdapBindRedactor` (`0x30`). Both are the SOCKS
  library's own parsers; no new project reference.
- Choices where it differs from the origin servers: the MQTT Remaining Length is rewritten for 3.1.1 too, so no length
  leaks; a SASL bind's whole credentials field is masked rather than only `PLAIN`'s, one rule with nothing to miss;
  unwalkable `CONNECT`s and binds fail closed.
- Test bytes: curl 8.21.0's `CONNECT` and simple bind, copied from the MQTT and LDAP servers' recorded fixtures; MQTT 5
  bytes are written from the specification, as pinned curl speaks 3.1.1 only. Nothing new was measured.
- Pipeline: planned, tested and implemented in the session; reviewed by `code-reviewer` (no behaviour defect; its
  documentation points were fixed). No conformance run: only the exchange log changes, no byte on the wire.
- The `--aihelp` and manual sentences that still call these two passwords not withheld are BL-1723's (it depends on
  this task; `Surl.Cli` is BL-1735's while it is in Doing).
- MQTT 5 Authentication Data is filed as BL-1744.
- Verified: `-warnaserror` build 0 warnings, format clean, fast tests 29360 passed, 0 failed, 3 skipped; SOCKS tests
  365 passed, library line and branch coverage 100%.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. MQTT 3.1.1 and 5 CONNECT passwords and LDAP bind secrets through a SOCKS tunnel are logged as ****
