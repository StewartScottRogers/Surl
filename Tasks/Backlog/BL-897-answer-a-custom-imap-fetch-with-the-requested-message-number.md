---
id: BL-897
title: Answer a custom IMAP FETCH with the requested message number as upstream's test server does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-05
completed:
---
# BL-897 — Answer a custom IMAP FETCH with the requested message number as upstream's test server does

## Goal

The IMAP upstream-test-case server half answers a custom-request `FETCH` (`-X` / `--request`) with the
untagged line upstream's test server writes, echoing the requested message number and item list, and any
case whose expected line is only an arbitrary quirk of that server is listed as an exclusion instead.

## Context

- Found by BL-879's Windows run of `UpstreamTestCasesAgainstSurlTests` on the reference pin, 2026-11-05.
- Cases (files under `Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/data/test<N>`):
  - `test841`: `-X 'FETCH 123 BODY[1]'`; expects `* 123 FETCH (BODY[1] {70}`; Surl sends `* 1 FETCH`.
  - `test3206`: `-X 'FETCH 456 ("fake {50}" BODY[TEXT])'`; expects
    `* 456 FETCH (("fake {50}" BODY[TEXT]) {7201}`; Surl sends `* 1 FETCH ("fake {50}" BODY[TEXT] {7201}`.
  - `test1847`: `--request 'UID FETCH 1 BODY[]'`; expects `* FETCH FETCH (1 BODY[] {71}`; Surl sends
    `* 1 FETCH (UID 1 BODY[] {71}`.
- Read upstream's test server, `tests/ftpserver.pl` at tag `curl-8_21_0`
  (https://github.com/curl/curl/blob/curl-8_21_0/tests/ftpserver.pl), sub `FETCH_imap` and how it splits the
  custom request's arguments, and reproduce its line for 841 and 3206. Where the expected line is purely that
  server's arbitrary quirk (likely 1847, where its argument split puts `FETCH` in the number's place), list the
  case in `Documentation/Conformance/Upstream-Test-Case-Exclusions.md` under ADR-0080 decision 5 with the
  reason, instead of reproducing it. Expected bytes come only from the case files (ADR-0003, ADR-0080); never
  from the Curl port.
- Where to work: `Protocols/Mail/Surl.Protocol.Imap.UnitLibrary/ImapSession.UpstreamTestCase.cs` and
  `ImapUpstreamTestCaseServer.cs`.
- Stand-in: none applies. This is the conformance half, not a stand-in feature; no shared store or event.

## Acceptance criteria

- [ ] Cases 841 and 3206 (and 1847 unless excluded) pass in a Windows run of `UpstreamTestCasesAgainstSurlTests`
      (`TestCategory=Integration`) with the reference pin from `UpstreamCurlBuilds.json`; Notes record the pin's
      SHA-256, the date, and for 1847 whether it was reproduced or excluded and why.
- [ ] The passing cases are added to `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`, and
      the `win-x64` row of `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` states the new counts;
      the fast pass-rate guard test is green.
- [ ] Any excluded case is listed in `Documentation/Conformance/Upstream-Test-Case-Exclusions.md` with the
      `ftpserver.pl` behaviour it would have to copy, and the run reports it as excluded rather than failed.
- [ ] `Surl.Protocol.Imap.UnitTests` has a unit test per reproduced case replaying its client bytes from the
      case file and asserting the untagged FETCH line the case expects.
- [ ] `dotnet build Protocols/Mail/Surl.Protocol.Imap.UnitTests -warnaserror` is clean,
      `dotnet test --filter "TestCategory!=Integration"` is green, and `Surl.Protocol.Imap.UnitLibrary` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
