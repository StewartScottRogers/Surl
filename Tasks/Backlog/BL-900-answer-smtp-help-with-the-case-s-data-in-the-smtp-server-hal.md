---
id: BL-900
title: Answer SMTP HELP with the case's data in the SMTP server half
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-05
completed:
---
# BL-900 — Answer SMTP HELP with the case's data in the SMTP server half

## Goal

The SMTP upstream-test-case server half answers `HELP` with the case's `<data>` instead of its own 214 line,
so upstream curl prints exactly the reply the case expects.

## Context

- Found by BL-879's Windows run of `UpstreamTestCasesAgainstSurlTests` on the reference pin, 2026-11-05.
- Case: `Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/data/test928`. Its `<data>` holds the HELP
  reply ("214-This server supports the following commands:" then "214 HELO EHLO ..."); Surl's half sends its
  own single 214 line, so curl's stdout differs.
- Check how upstream's test server sends it: `tests/ftpserver.pl` at tag `curl-8_21_0`
  (https://github.com/curl/curl/blob/curl-8_21_0/tests/ftpserver.pl), sub `HELP_smtp`, including when a case has
  no `<data>`. Expected bytes come only from the case file (ADR-0003, ADR-0080); never from the Curl port.
- Where to work: `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpSession.UpstreamTestCase.cs` and
  `SmtpUpstreamTestCaseServer.cs`; the case's reply parts reach the half through `MailUpstreamTestCaseReplies`.
- Stand-in: none applies. This is the conformance half, not a stand-in feature; no shared store or event.

## Acceptance criteria

- [ ] Case 928 passes in a Windows run of `UpstreamTestCasesAgainstSurlTests` (`TestCategory=Integration`)
      with the reference pin from `UpstreamCurlBuilds.json`; Notes record the pin's SHA-256 and the date.
- [ ] `928` is added to `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`, and the `win-x64`
      row of `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` states the new counts; the fast
      pass-rate guard test is green.
- [ ] `Surl.Protocol.Smtp.UnitTests` has a unit test that replays case 928's client bytes and asserts the half
      sends the case's `<data>` HELP reply byte for byte, and one for the no-`<data>` path.
- [ ] `dotnet build Protocols/Mail/Surl.Protocol.Smtp.UnitTests -warnaserror` is clean,
      `dotnet test --filter "TestCategory!=Integration"` is green, and `Surl.Protocol.Smtp.UnitLibrary` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
