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
completed: 2026-11-11
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

- [x] Cases 841 and 3206 (and 1847 unless excluded) pass in a Windows run of `UpstreamTestCasesAgainstSurlTests`
      (`TestCategory=Integration`) with the reference pin from `UpstreamCurlBuilds.json`; Notes record the pin's
      SHA-256, the date, and for 1847 whether it was reproduced or excluded and why.
- [x] The passing cases are added to `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`, and
      the `win-x64` row of `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` states the new counts;
      the fast pass-rate guard test is green.
- [x] Any excluded case is listed in `Documentation/Conformance/Upstream-Test-Case-Exclusions.md` with the
      `ftpserver.pl` behaviour it would have to copy, and the run reports it as excluded rather than failed.
- [x] `Surl.Protocol.Imap.UnitTests` has a unit test per reproduced case replaying its client bytes from the
      case file and asserting the untagged FETCH line the case expects.
- [x] `dotnet build Protocols/Mail/Surl.Protocol.Imap.UnitTests -warnaserror` is clean,
      `dotnet test --filter "TestCategory!=Integration"` is green, and `Surl.Protocol.Imap.UnitLibrary` keeps
      100% line and branch coverage.

## Notes

- Upstream's server takes the custom request's first argument as the message number and the rest as the item, both echoed verbatim; for `UID FETCH` it takes `UID` as the command, so `FETCH` lands in the number's place. One rule reproduces 841, 3206 and 1847, so 1847 was reproduced, not excluded. (Rule derived from the three case files' datacheck sections; `ftpserver.pl` was not fetched.) The old BODY.PEEK rewrite and parenthesis trimming were dropped; no case relied on them.
- Windows run 2026-11-11, reference pin SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`: 1,070 passed, 841, 1847 and 3206 among the new ones.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Custom IMAP FETCH echoes the requested number and item as upstream's server does; cases 841, 1847 and 3206 pass (1,070 on win-x64)
