---
id: BL-1753
title: Withhold a SOCKS-tunnelled PASS or ACCT argument when a tab separates it from the command word
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1767]
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1753 — Withhold a SOCKS-tunnelled PASS or ACCT argument when a tab separates it from the command word

## Goal

The SOCKS tunnel's exchange log writes the argument as `****` on a tunnelled `PASS<tab><password>` or
`ACCT<tab><account>` line.

## Context

- Found while working BL-1749. `SocksTunnelPasswordAndAuthorizationLineRule` matches `"PASS "` and `"ACCT "` with a
  space, so a tab-separated line is logged with the secret. Read from the code; no test reproduces it yet.
- BL-1752 and BL-1751 are the same gap in the FTP and POP3 servers' own logs. Protocol libraries never reference each
  other.

## Acceptance criteria

- [ ] A named test in `Surl.Protocol.Socks.UnitTests` shows the argument masked on a tunnelled `PASS<tab><password>`.
- [ ] A named test shows it masked on a tunnelled `ACCT<tab><account>`.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Now depends on BL-1767 (Stewart: 'prioritise 1767'): fix this in the shared tunnel line rules BL-1767 moves into Surl.LineProtocol, not in a per-server copy.
