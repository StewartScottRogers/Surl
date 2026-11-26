---
id: BL-1740
title: Withhold an IMAP LOGIN password when more than one space precedes the LOGIN word
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1740 — Withhold an IMAP LOGIN password when more than one space precedes the LOGIN word

## Goal

`A1  LOGIN alice <password>`, with more than one space between the tag and `LOGIN`, is logged by the IMAP server with
the password as `****`.

## Context

- Found while working BL-1738. `ImapLineRedactionRule.TryFindLoginArguments` takes the bytes up to the second space as
  the command word, so a doubled space after the tag makes the word empty and the whole line, password included, is
  logged. `SocksTunnelImapLoginLineRule` already skips the run of spaces; ADR-0183 amendment 2 does the same for SASL.

## Acceptance criteria

- [ ] Named tests in `Surl.Protocol.Imap.UnitTests` show the password masked after two spaces between the tag and
      `LOGIN`, for an atom, a quoted string and a literal password.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
