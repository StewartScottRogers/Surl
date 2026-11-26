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
completed: 2026-11-25
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

- [x] A named test in `Surl.Protocol.Imap.UnitTests` shows the password masked on a `LOGIN` line that starts with a
      space.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

- `TryFindLoginArguments` now skips leading spaces before it reads the tag. Tests:
  `Login_WithASpaceBeforeTheTagAndAnAtomPassword_IsMasked` and its quoted and literal siblings; the three failed
  before the fix.
- Choice: a line that starts with a space and whose first word is `LOGIN` is still read as a `LOGIN` with no tag, as it
  was before this change (`Login_WithASpaceInPlaceOfTheTag_IsMasked`). Skipping the spaces alone would have taken
  `LOGIN` for the tag and logged that password. No server byte changes, so no ADR and no upstream curl measurement.
- Verified 2026-11-25: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests 0
  failed (IMAP 826 passed), `Surl.Protocol.Imap.UnitLibrary` 100% line, 100% branch, 0 failing members.
- Filed BL-1748: a `LOGIN` line with no tag and no leading space, or with tab separators, is still logged in clear.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. An IMAP LOGIN line that starts with a space is logged with the password as ****
