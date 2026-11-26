---
id: BL-1750
title: Withhold a SOCKS-tunnelled IMAP LOGIN password when the line has no tag or uses tabs
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1767]
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1750 — Withhold a SOCKS-tunnelled IMAP LOGIN password when the line has no tag or uses tabs

## Goal

The SOCKS tunnel's exchange log writes the password as `****` on an IMAP `LOGIN` line with no tag, and on one whose
words are separated by tabs.

## Context

- Found while working BL-1748. BL-1767 moved the rule to `Surl.LineProtocol`'s `TunnelImapLoginLineRule`, shared with
  the HTTP proxy. It now reads tab-separated words, pinned for SOCKS by a row of
  `SocksTunnelMailRedactionTests.Imap_EachLoginForm_IsLoggedMasked` (the second criterion). It still finds `LOGIN` only
  as the second word.
- `ImapLineRedactionRule` and ADR-0183 amendment 3 hold the rules the IMAP server's own log follows. Protocol libraries
  never reference each other.

## Acceptance criteria

- [ ] A named test in `Surl.Protocol.Socks.UnitTests` shows the password masked on a `LOGIN` line with no tag.
- [ ] A named test shows the password masked on a `LOGIN` line whose words are separated by tabs.
- [ ] A named test shows the password masked on `LOGIN LOGIN alice <password>`.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Now depends on BL-1767 (Stewart: 'prioritise 1767'): fix this in the shared tunnel line rules BL-1767 moves into Surl.LineProtocol, not in a per-server copy.
- 2026-11-26: Backlog -> Doing.
