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
completed: 2026-11-26
---
# BL-1753 — Withhold a SOCKS-tunnelled PASS or ACCT argument when a tab separates it from the command word

## Goal

The SOCKS tunnel's exchange log writes the argument as `****` on a tunnelled `PASS<tab><password>` or
`ACCT<tab><account>` line.

## Context

- Found while working BL-1749. `Surl.LineProtocol`'s `TunnelPasswordAndCredentialHeaderLineRule` (shared by the SOCKS
  server and the HTTP proxy since BL-1767) matches `"PASS "` and `"ACCT "` with a space, so a tab-separated line is
  logged with the secret. Read from the code; no test reproduces it yet.
- BL-1752 and BL-1751 are the same gap in the FTP and POP3 servers' own logs. Protocol libraries never reference each
  other.

## Acceptance criteria

- [x] A named test in `Surl.Protocol.Socks.UnitTests` shows the argument masked on a tunnelled `PASS<tab><password>`.
- [x] A named test shows it masked on a tunnelled `ACCT<tab><account>`.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

- Tests: `Tunnel_PassSeparatedByATab_IsMaskedKeepingTheTab` and `Tunnel_AcctSeparatedByATab_IsMaskedKeepingTheTab` in
  `SocksExchangeLogRedactionTests`; both failed before the fix, as did the four tab rows added to
  `TunnelPasswordAndCredentialHeaderLineRuleTests`.
- The fix is in the shared `TunnelPasswordAndCredentialHeaderLineRule`, so the HTTP proxy's tunnel log changes too.
  No Socks production code changed.
- Choice: the one space or tab after the command word is logged as received and everything after it is `****`, as the
  FTP server's log does (BL-1752). Recorded as ADR-0182 amendment 30.
- No upstream curl measurement: curl sends a single space, and only what the log writes changes.
- Verified 2026-11-26: `dotnet build -warnaserror` 0 warnings, format check clean, fast tests 29976 passed, 0 failed,
  3 skipped; `Measure-CodeQuality.ps1 -Library Surl.LineProtocol.UnitLibrary` 0 failing members.
- Set aside, filed as BL-1783: a `PASS` or `ACCT` line that starts with spaces or tabs is still logged with its secret.

## Log

- 2026-11-25: Created.
- 2026-11-25: Now depends on BL-1767 (Stewart: 'prioritise 1767'): fix this in the shared tunnel line rules BL-1767 moves into Surl.LineProtocol, not in a per-server copy.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. The tunnel exchange log masks the PASS or ACCT argument when a tab separates it from the command word
