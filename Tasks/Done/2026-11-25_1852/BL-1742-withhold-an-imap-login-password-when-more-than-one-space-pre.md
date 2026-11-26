---
id: BL-1742
title: Withhold an IMAP LOGIN password when more than one space precedes the LOGIN word
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1742 — Withhold an IMAP LOGIN password when more than one space precedes the LOGIN word

## Goal

`A1  LOGIN alice <password>`, with more than one space between the tag and `LOGIN`, is logged by the IMAP server with
the password as `****`.

## Context

- Found while working BL-1738. `ImapLineRedactionRule.TryFindLoginArguments` takes the bytes up to the second space as
  the command word, so a doubled space after the tag makes the word empty and the whole line, password included, is
  logged. `SocksTunnelImapLoginLineRule` already skips the run of spaces; ADR-0183 amendment 2 does the same for SASL.

## Acceptance criteria

- [x] Named tests in `Surl.Protocol.Imap.UnitTests` show the password masked after two spaces between the tag and
      `LOGIN`, for an atom, a quoted string and a literal password.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

- `TryFindLoginArguments` now skips the whole run of spaces after the tag, as `SocksTunnelImapLoginLineRule` does. No
  ADR: ADR-0183 amendment 1 already says `LOGIN`'s password is `****`. No byte Surl sends changes, so nothing was
  measured from upstream curl.
- Tests: `Login_WithTwoSpacesAfterTheTagAndAnAtomPassword_IsMasked`, `..._AQuotedPassword_IsMasked`,
  `..._APasswordLiteral_IsMasked` (all three failed before the fix), and `TagFollowedOnlyBySpaces_IsLoggedAsSent`.
- Verified 2026-11-25: build 0 warnings, `dotnet format` clean, fast tests green (IMAP 821 passed),
  `Measure-CodeQuality.ps1 -Library Surl.Protocol.Imap.UnitLibrary` 0 failing members.
- A line that starts with a space still logs its password; filed as BL-1747.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The IMAP exchange log writes the LOGIN password as **** however many spaces follow the tag
