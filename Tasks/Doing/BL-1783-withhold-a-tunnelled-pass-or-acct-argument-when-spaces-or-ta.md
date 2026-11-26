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
completed:
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

- [ ] A named test in `Surl.Protocol.Socks.UnitTests` shows the argument masked on a tunnelled `<space>PASS <password>`.
- [ ] A named test shows it masked on a tunnelled `<tab>ACCT<tab><account>`.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
