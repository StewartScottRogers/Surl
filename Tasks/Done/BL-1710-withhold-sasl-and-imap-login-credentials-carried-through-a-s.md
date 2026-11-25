---
id: BL-1710
title: Withhold SASL and IMAP LOGIN credentials carried through a SOCKS tunnel from the exchange log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1709]
touches: [Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1710 — Withhold SASL and IMAP LOGIN credentials carried through a SOCKS tunnel from the exchange log

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

- [x] Named fast tests show an `AUTH PLAIN <base64>` line and an `AUTH LOGIN` exchange's continuation lines after
      `334` logged as `****` through a granted tunnel.
- [x] Named tests show an IMAP tagged `LOGIN` password and an `AUTHENTICATE` continuation after `+` logged as `****`.
- [x] Named tests show a non-secret line of each protocol logged as received.
- [x] ADR-0182 Amendment 6's limits and the SOCKS `CLAUDE.md` say what is now masked.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the SOCKS library stays at 100% line and branch
      coverage.

## Notes

- Tests: `SocksTunnelMailRedactionTests` (`Smtp_AuthPlainInitialResponse_IsLoggedMasked`,
  `Smtp_AuthLoginContinuationsAfter334_AreLoggedMasked`, `Imap_TaggedLogin_HasThePasswordMasked`,
  `Imap_AuthenticateContinuationAfterPlus_IsLoggedMasked`, `ALineOfEachProtocolWithNoSecret_IsLoggedAsReceived`).
- Planned and implemented in the lane session, with one `code-reviewer` pass. No upstream curl measurement: no byte
  surl sends or expects changes, only its own log.
- Decisions are ADR-0182 Amendment 9. The SOCKS library now references `Surl.LineProtocol` (allowed by ADR-0002
  decision 3) for `SaslLineRedactionRule`; it keeps its own IMAP `LOGIN` rule.
- The review found, and this task fixed: a bare `+` or `334` continuation left the answer logged; a tunnel opening
  with a numbered IMAP tag was logged raw; two spaces after an IMAP tag hid `LOGIN`; a literal made of bare line
  feeds could be counted short.
- Filed BL-1737: `SaslLineRedactionRule` logs an initial response that follows two spaces (outside `touches`).
- `--aihelp` (`logging`, `security`) and the manual still list SASL and IMAP `LOGIN` as not withheld in a tunnel. They
  are in `Surl.Cli`, which BL-1721 held in Doing; BL-1723, which depends on this task, updates them.
- Final gates: format clean, `dotnet build -warnaserror` clean, fast tests green (83 test assemblies; SOCKS 302),
  SOCKS library 100% line and branch, 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. SMTP, IMAP and POP3 AUTH, AUTHENTICATE and LOGIN credentials through a SOCKS tunnel are logged as ****
