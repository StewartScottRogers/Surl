---
id: BL-1747
title: Withhold an IMAP LOGIN password when a space precedes the tag
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1747 — Withhold an IMAP LOGIN password when a space precedes the tag

## Goal

` A1 LOGIN alice <password>`, a line that starts with a space, is logged by the IMAP server with the password as
`****`.

## Context

- Found while working BL-1742. `ImapLineRedactionRule.TryFindLoginArguments` takes the tag as the bytes before the
  first space, so a leading space makes the tag empty and the tag the command word, and the whole line, password
  included, is logged. The server refuses such a line (`ImapArguments.ReadTag`), but the client has already sent the
  password.

## Acceptance criteria

- [ ] A named test in `Surl.Protocol.Imap.UnitTests` shows the password masked on a `LOGIN` line that starts with a
      space.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
