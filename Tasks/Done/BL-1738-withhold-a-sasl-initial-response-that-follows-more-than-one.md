---
id: BL-1738
title: Withhold a SASL initial response that follows more than one space in SaslLineRedactionRule
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Documentation/Planning/Decisions/ADR-0183-a-sasl-line-redaction-rule-withholds-initial-responses-and-continuation-answers.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1738 — Withhold a SASL initial response that follows more than one space in SaslLineRedactionRule

## Goal

`AUTH PLAIN  <base64>`, with more than one space before the response (or the mechanism), is logged with the response
as `****`.

## Context

- Found in the code review of BL-1710. `SaslLineRedactionRule.TryFindInitialResponse` splits a line on single spaces,
  so a doubled space makes the third token empty and the response is logged. The SMTP, IMAP and POP3 servers and the
  SOCKS tunnel (ADR-0182 Amendment 9) all use this rule. curl sends single spaces.

## Acceptance criteria

- [x] Named tests in `Surl.LineProtocol.UnitTests` show the response masked after two spaces before the mechanism and
      before the response, with and without an IMAP tag.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

- Decision (ADR-0183 amendment 2): a run of spaces separates two tokens, and leading spaces are skipped, so the log
  masks the response however the client spaced the line. Spaces are logged as received.
- Added ADR-0183 to `touches` to record that decision; no task in Doing named it.
- Tests: `Redact_InitialResponseAfterARunOfSpaces_IsMasked`, `Redact_ImapInitialResponseAfterARunOfSpaces_IsMasked`,
  `Redact_SpacesBeforeTheCommandWord_StillMasksTheInitialResponse`. 10 cases failed before the fix.
- Verified: build `-warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests green,
  `Surl.LineProtocol.UnitLibrary` 100% line and branch, 0 failing members.
- Filed BL-1742: the IMAP server's own `LOGIN` rule logs the password after two spaces before `LOGIN`.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. SaslLineRedactionRule masks a SASL initial response however many spaces separate the line's words
