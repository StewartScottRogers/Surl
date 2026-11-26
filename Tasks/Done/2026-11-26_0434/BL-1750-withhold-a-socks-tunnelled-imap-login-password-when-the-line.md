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
completed: 2026-11-26
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

- [x] A named test in `Surl.Protocol.Socks.UnitTests` shows the password masked on a `LOGIN` line with no tag.
- [x] A named test shows the password masked on a `LOGIN` line whose words are separated by tabs.
- [x] A named test shows the password masked on `LOGIN LOGIN alice <password>`.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

- Fix is in the shared `TunnelImapLoginLineRule` (BL-1767), so the SOCKS tunnel and the HTTP proxy `CONNECT` tunnel
  both get it. `LOGIN` may be the first or second word; blanks before the first word are skipped; `LOGIN LOGIN` has
  everything after both masked. This follows the IMAP server's own rule (ADR-0183 amendment 3). Recorded as ADR-0182
  amendment 29.
- Chosen: skipping leading blanks also masks ` a LOGIN app <password>`, which was logged as received. It follows from
  reading the first word the way the IMAP rule does, and masking more is the safe side. Cost: a line of another
  protocol starting with the word `login` loses log detail.
- A SOCKS tunnel whose first byte is a blank is still not read as text (amendment 9, point 1); unchanged.
- No upstream curl measurement: curl sends a tagged `LOGIN` only, and this changes the log, not bytes on the wire.
- Tests: `Imap_LoginWithNoTag_HasThePasswordMasked`, `Imap_LoginSeparatedByTabs_HasThePasswordMasked`,
  `Imap_LoginWordTwiceBeforeTheUserName_HasEverythingAfterBothMasked` and
  `Imap_LiteralPasswordOnALoginWithNoTag_IsLoggedMasked` (SOCKS), plus rows in `TunnelImapLoginLineRuleTests`.
- Results 2026-11-26: build `-warnaserror` 0 warnings, format check clean, 29965 fast tests passed (3 skipped);
  `Surl.LineProtocol.UnitLibrary` and `Surl.Protocol.Socks.UnitLibrary` at 100% line and branch.
- `--aihelp` and the manual still name this line as logged; BL-1764 (depends on this task) owns that text.

## Log

- 2026-11-25: Created.
- 2026-11-25: Now depends on BL-1767 (Stewart: 'prioritise 1767'): fix this in the shared tunnel line rules BL-1767 moves into Surl.LineProtocol, not in a per-server copy.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. Both proxy tunnels' exchange logs mask the password of an IMAP LOGIN line with no tag, with tabs, or reading LOGIN LOGIN.
