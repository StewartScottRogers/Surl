---
id: BL-1783
title: Withhold a tunnelled PASS or ACCT argument when spaces or tabs come before the command word
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1753]
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1783 — Withhold a tunnelled PASS or ACCT argument when spaces or tabs come before the command word

## Goal

The SOCKS tunnel's exchange log writes the argument as `****` on a `PASS` or `ACCT` line that starts with spaces or
tabs.

## Context

- Found while working BL-1753. `TunnelPasswordAndCredentialHeaderLineRule.IsPasswordCommand` in `Surl.LineProtocol`
  matches only at the start of the line, so `<space>PASS <password>` is logged with the password (ADR-0182 amendment 30,
  accepted limit). Read from the code; no test reproduces it yet.
- BL-1756 closed the same gap in the POP3 server's own log. A line starting with a space or a tab after a withheld
  header is already masked whole as its continuation.
- The HTTP proxy uses the same rule, so the fix changes its log as well.

## Acceptance criteria

- [x] A named test in `Surl.Protocol.Socks.UnitTests` shows the argument masked on a tunnelled `<space>PASS <password>`.
- [x] A named test shows it masked on a tunnelled `<tab>ACCT<tab><account>`.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

- Tests: `SocksExchangeLogRedactionTests.Tunnel_PassAfterASpace_IsLoggedMasked` and
  `Tunnel_AcctAfterATabAndSeparatedByATab_IsLoggedMasked`; both failed before the fix. More rows in
  `TunnelPasswordAndCredentialHeaderLineRuleTests`.
- Decided (ADR-0182 amendment 34): every space and tab at the line's start is skipped before the command word, as
  amendment 33 does before a header name. The blanks, the word and its ending byte are logged as received.
- The HTTP proxy's tunnel log changes too, through the shared rule; no HTTP proxy file needed a change.
- Still accepted: a tunnel whose first byte is a space or a tab is not read as text (amendment 9). A command after a
  bare carriage return is BL-1785.
- `--aihelp` and the manual still name the leading-blank `PASS`/`ACCT` line as logged; BL-1784, which waits on this
  task, rewrites those sentences.
- 2026-11-26 results: build 0 warnings; fast tests 30035 passed, 0 failed, 3 skipped; `Surl.LineProtocol.UnitLibrary`
  has 0 failing members in `Measure-CodeQuality.ps1`.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. A tunnelled PASS or ACCT line starting with spaces or tabs has its argument logged as **** in the SOCKS and HTTP proxy exchange logs
