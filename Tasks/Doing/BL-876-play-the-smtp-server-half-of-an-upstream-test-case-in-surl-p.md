---
id: BL-876
title: Play the SMTP server half of an upstream test case in Surl.Protocol.Smtp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-875]
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-876 — Play the SMTP server half of an upstream test case in Surl.Protocol.Smtp

## Goal

`SmtpUpstreamTestCaseServer` (public, in `Surl.Protocol.Smtp`) is Surl's own `SmtpProtocolServer` playing one
upstream test case through BL-875's `MailUpstreamTestCaseReplies`. Unit tests drive it without a network.

## Context

- Part of BL-395's split (timed out after 120 min; Stewart asked for the split 2026-11-05). BL-875 supplies
  `MailUpstreamTestCaseReplies`. BL-879 composes this half into the conformance run.
- Decision: ADR-0080 decision 2. The half is Surl's own server, reading every line with its own reader and
  answering with its own replies. The case changes only what FILEFORMAT.md (tag `curl-8_21_0`, lines 367 to
  395) makes the case's:
  - `REPLY welcome` replaces the greeting, and a `REPLY`'d line (a command or a SASL response) is answered
    with the case's text and not run.
  - `EHLO` lists the case's `CAPA` capabilities and an `AUTH` line with its mechanisms, and nothing of the
    server's own.
  - `MAIL` needs no login, as upstream's test server needs none.
  - A `DATA` body is recorded as sent (dot-stuffed, with its ending `.` CRLF) in `UploadedBytes`, not
    delivered.
  - `VRFY` and `EXPN` answer with the case's data for the `EHLO` name when it has any.
  - Every command line is recorded in `ReceivedBytes`.
  Logins go to the `AnonymousAuthenticationPolicy` passed in. FTP precedent: BL-394's `FtpUpstreamTestCaseServer`
  and `FtpCommandResponder.UpstreamTestCase.cs`.
- With no case (`caseReplies` null), `SmtpProtocolServer` behaves exactly as before (ADR-0053). Every existing
  SMTP test passes unchanged.
- **Reuse the stash.** `git stash list` and find `darkfactory BL-395`; call it `stash@{N}`. Take these files:
  - Untracked files: `git checkout "stash@{N}^3" -- Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpSession.UpstreamTestCase.cs Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpUpstreamTestCaseServer.cs`,
    then `git reset` those paths.
  - Tracked edits: `git diff "stash@{N}^1" "stash@{N}" -- Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpProtocolServer.cs Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpSession.cs | git apply -3`.
    These add the internal constructor taking `MailUpstreamTestCaseReplies?`, the partial class, the
    greeting, line and `EHLO` hooks, and `IsMailAllowedAsync`'s case bypass.
  The code compiled in the lane but has **no tests**. Ignore the stash's other files. Do not apply or drop the
  stash.
- Stand-in (ADR-0124): none applies. The half uses an empty `MailboxStore` it owns and delivers nothing, so it
  has no shared store, events or faults.

## Acceptance criteria

- [ ] `Protocols/Mail/Surl.Protocol.Smtp.UnitTests` has `SmtpUpstreamTestCaseServerTests`, which drive the half
      over an in-memory connection with inline cases. They cover `REPLY welcome`, a `REPLY`'d command not being
      run, a `COUNT`-limited reply, `REPLYLF`, `EHLO` listing `CAPA` and `AUTH`, `MAIL` without a login, a
      `DATA` body landing in `UploadedBytes` and not in the store, `VRFY` answered from the case's data, and
      `ReceivedBytes` holding every command line with CRLF.
- [ ] A named test shows that `SmtpProtocolServer` composed without a case still answers `EHLO` with its own
      extensions and still refuses `MAIL` before a login where ADR-0053 refuses it.
- [ ] `dotnet build Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary -warnaserror` is clean,
      `dotnet test --filter "TestCategory!=Integration"` is green, and `Surl.Protocol.Smtp.UnitLibrary` keeps 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Split from BL-395 (timed out after 120 min; Stewart asked for the split 2026-11-05).
- 2026-11-05: Backlog -> Doing.
