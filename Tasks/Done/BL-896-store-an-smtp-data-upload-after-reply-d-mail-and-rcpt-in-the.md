---
id: BL-896
title: Store an SMTP DATA upload after REPLY'd MAIL and RCPT in the SMTP server half
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-896 — Store an SMTP DATA upload after REPLY'd MAIL and RCPT in the SMTP server half

## Goal

The SMTP upstream-test-case server half records the DATA upload in `MailUpstreamTestCaseReplies.UploadedBytes`
even when the case's `<servercmd>` answered MAIL and RCPT with `REPLY` rather than letting the half run them.

## Context

- Found by BL-879's Windows run of `UpstreamTestCasesAgainstSurlTests` on the reference pin, 2026-11-05.
- Case: `Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/data/test3215`. Its `<servercmd>` has
  `REPLY MAIL 250 Ok` and `REPLY RCPT 250 Ok`; curl sends `MAIL FROM` with `RET=HDRS` and `RCPT TO` with
  `NOTIFY=...`. `<protocol>` passes including DATA, but `<upload>` is empty: the half does not store the
  message, because the REPLY path skips whatever MAIL and RCPT normally set up for DATA.
- Upstream's test server, `tests/ftpserver.pl` at tag `curl-8_21_0`
  (https://github.com/curl/curl/blob/curl-8_21_0/tests/ftpserver.pl), sub `DATA_smtp`, stores the upload
  regardless of how MAIL and RCPT were answered; read it to confirm. Expected bytes come only from the case
  file (ADR-0003, ADR-0080); never from the Curl port.
- Where to work: `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpSession.UpstreamTestCase.cs` and
  `SmtpUpstreamTestCaseServer.cs`.
- Stand-in: none applies. This is the conformance half, not a stand-in feature; no shared store or event.

## Acceptance criteria

- [x] Case 3215 passes in a Windows run of `UpstreamTestCasesAgainstSurlTests` (`TestCategory=Integration`)
      with the reference pin from `UpstreamCurlBuilds.json`, including its `<upload>` check; Notes record the
      pin's SHA-256 and the date.
- [x] `3215` is added to `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`, and the `win-x64`
      row of `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` states the new counts; the fast
      pass-rate guard test is green.
- [x] `Surl.Protocol.Smtp.UnitTests` has a unit test that replays case 3215's client bytes with `REPLY MAIL`
      and `REPLY RCPT` and asserts the uploaded bytes equal the case's `<upload>`.
- [x] `dotnet build Protocols/Mail/Surl.Protocol.Smtp.UnitTests -warnaserror` is clean,
      `dotnet test --filter "TestCategory!=Integration"` is green, and `Surl.Protocol.Smtp.UnitLibrary` keeps
      100% line and branch coverage.

## Notes

- Cause: a REPLY'd MAIL or RCPT is answered and not run, so no transaction existed and DATA answered 503. Fix in `SmtpSession.UpstreamTestCase.cs`: a REPLY'd MAIL opens the transaction, a REPLY'd RCPT counts as a recipient (matches upstream DATA_smtp, which stores regardless).
- Windows Integration run of `UpstreamTestCasesAgainstSurlTests` on 2026-11-11 with the reference pin `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`: green, 3215 newly passing (1,067 of 1,126 runnable). Passing list and pass-rate row updated.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. SMTP upstream-case half stores the DATA upload after REPLY'd MAIL and RCPT; case 3215 passes
