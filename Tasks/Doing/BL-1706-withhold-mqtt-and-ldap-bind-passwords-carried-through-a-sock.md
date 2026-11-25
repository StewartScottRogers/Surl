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
completed:
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

- [ ] Named fast tests show an MQTT 3.1.1 and an MQTT 5 `CONNECT` password logged as `****` through a granted tunnel,
      also when split across reads at every byte.
- [ ] A named test shows the same for an LDAP simple bind.
- [ ] Named tests show a tunnel starting `0x16`, and a malformed `0x10` and `0x30` tunnel, each handled without the
      redactor holding more than 64 KiB.
- [ ] ADR-0182 Amendment 6's limits and the SOCKS `CLAUDE.md` say what is now masked.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the SOCKS library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
