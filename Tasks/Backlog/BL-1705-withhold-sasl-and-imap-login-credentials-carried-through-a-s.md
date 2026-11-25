---
id: BL-1705
title: Withhold SASL and IMAP LOGIN credentials carried through a SOCKS tunnel from the exchange log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1704]
touches: [Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1705 — Withhold SASL and IMAP LOGIN credentials carried through a SOCKS tunnel from the exchange log

## Goal

SMTP, IMAP and POP3 `AUTH`, IMAP `AUTHENTICATE` and `LOGIN` credentials carried through a SOCKS tunnel are logged as
`****`, including the SASL continuation lines after `334` or `+`.

## Context

- BL-1697 masks only `PASS`, `ACCT`, `Authorization` and `Proxy-Authorization` in a tunnel (ADR-0182 Amendment 6,
  accepted limits). The rule is `SocksTunnelLineRedactionRule`.
- The origin servers' rules are the model: `Surl.LineProtocol`'s `SaslLineRedactionRule` and the SMTP, IMAP and POP3
  line rules. The SOCKS library may reference only what ADR-0002 decision 3's table allows.
- `SocksTunnelBytesRedactor.ObserveSent` already passes the origin's bytes to the rule.

## Acceptance criteria

- [ ] Named fast tests show an `AUTH PLAIN <base64>` line and an `AUTH LOGIN` exchange's continuation lines after
      `334` logged as `****` through a granted tunnel.
- [ ] Named tests show an IMAP tagged `LOGIN` password and an `AUTHENTICATE` continuation after `+` logged as `****`.
- [ ] Named tests show a non-secret line of each protocol logged as received.
- [ ] ADR-0182 Amendment 6's limits and the SOCKS `CLAUDE.md` say what is now masked.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the SOCKS library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
