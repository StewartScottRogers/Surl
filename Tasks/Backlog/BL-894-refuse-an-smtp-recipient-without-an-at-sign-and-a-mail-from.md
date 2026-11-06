---
id: BL-894
title: Refuse an SMTP recipient without an at sign and a MAIL FROM whose SIZE exceeds CAPA SIZE in the SMTP server half
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-05
completed:
---
# BL-894 — Refuse an SMTP recipient without an at sign and a MAIL FROM whose SIZE exceeds CAPA SIZE in the SMTP server half

## Goal

The SMTP upstream-test-case server half refuses `RCPT TO` for a recipient with no `@`, and refuses
`MAIL FROM ... SIZE=<n>` when `<n>` exceeds the `SIZE` the case's `CAPA` advertised, so upstream curl QUITs
instead of carrying on to RCPT or DATA, as upstream's test server makes it do.

## Context

- Found by BL-879's Windows run of `UpstreamTestCasesAgainstSurlTests` on the reference pin, 2026-11-05.
- Cases (files under `Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/data/test<N>`):
  - `test913`: `CAPA` "SIZE 32"; curl sends `MAIL FROM:<...> SIZE=38`; the case expects curl to send `QUIT`
    next, so the server must refuse MAIL.
  - `test916`: `RCPT TO:<invalid>` must be refused, then curl QUITs.
  - `test918`: several recipients; the second, `<invalid>`, is refused and curl QUITs.
  - `test3006` and `test3007`: `--mail-rcpt-allowfails`; every recipient lacks `@`, all are refused, and curl
    QUITs without DATA.
- Today Surl accepts both commands and curl goes on to RCPT or DATA, so `<protocol>` differs.
- Reply codes: read upstream's test server, `tests/ftpserver.pl` at tag `curl-8_21_0`
  (https://github.com/curl/curl/blob/curl-8_21_0/tests/ftpserver.pl), subs `MAIL_smtp` and `RCPT_smtp`, and
  answer with the same codes and text. Expected client bytes come only from the case files (ADR-0003,
  ADR-0080); never from the Curl port.
- Where to work: `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpSession.UpstreamTestCase.cs` and
  `SmtpUpstreamTestCaseServer.cs`. The behaviour belongs to the upstream-test-case half only; the production
  SMTP server's own rules are not changed by this task.
- Stand-in: none applies. This is the conformance half that mimics upstream's test server, not a stand-in
  feature; it uses no shared store and publishes nothing new.

## Acceptance criteria

- [ ] Cases 913, 916, 918, 3006 and 3007 pass in a Windows run of `UpstreamTestCasesAgainstSurlTests`
      (`TestCategory=Integration`) with the reference pin from `UpstreamCurlBuilds.json`; Notes record the
      pin's SHA-256 and the date.
- [ ] The five case numbers are added to `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`,
      and the `win-x64` row of `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` states the new counts;
      the fast pass-rate guard test is green.
- [ ] `Surl.Protocol.Smtp.UnitTests` has a unit test per refusal (oversize `SIZE=`, recipient without `@`)
      replaying the client bytes of cases 913 and 916 from their case files and asserting the reply codes read
      from `ftpserver.pl`.
- [ ] `dotnet build Protocols/Mail/Surl.Protocol.Smtp.UnitTests -warnaserror` is clean,
      `dotnet test --filter "TestCategory!=Integration"` is green, and `Surl.Protocol.Smtp.UnitLibrary` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
