---
id: BL-1748
title: Withhold an IMAP LOGIN password when the line has no tag or uses tabs
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1748 — Withhold an IMAP LOGIN password when the line has no tag or uses tabs

## Goal

`LOGIN alice <password>` (no tag, no leading space) and a `LOGIN` line whose words are separated by tabs are logged by
the IMAP server with the password as `****`.

## Context

- Found while working BL-1747. `ImapLineRedactionRule.TryFindLoginArguments` splits words on spaces only and treats the
  first word as the tag unless the line starts with a space. `LOGIN alice <password>` therefore reads as tag `LOGIN`,
  command `alice`, and `A1<tab>LOGIN<tab>alice<tab><password>` reads as one word. The server refuses both lines, but the
  client has already sent the password.
- A line whose tag really is `LOGIN` (`LOGIN LOGIN alice <password>`) must keep masking the password.

## Acceptance criteria

- [ ] A named test in `Surl.Protocol.Imap.UnitTests` shows the password masked on a `LOGIN` line with no tag and no
      leading space.
- [ ] A named test shows the password masked on a `LOGIN` line whose words are separated by tabs.
- [ ] A named test shows the password masked on `LOGIN LOGIN alice <password>`.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
