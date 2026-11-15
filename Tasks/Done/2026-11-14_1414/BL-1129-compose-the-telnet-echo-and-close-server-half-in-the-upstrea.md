---
id: BL-1129
title: Compose the TELNET echo-and-close server half in the upstream test-case run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests, Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary, Protocols/Terminal/Surl.Protocol.Telnet.UnitTests, Documentation/Planning/Decisions/ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md]
requirement: FR-053
created: 2026-11-14
completed: 2026-11-14
---
# BL-1129 — Compose the TELNET echo-and-close server half in the upstream test-case run

## Goal

The upstream test-case run serves `%TELNETPORT` with `TelnetProtocolServer` in echo-and-close mode
(`TelnetUpstreamTestCaseReplies`), so upstream test 1452 passes on Windows, Linux and macOS.

## Context

- 1452 (`telnet://%HOSTIP:%TELNETPORT --upload-file -`) fails `timeout` on all three platforms:
  CI run 43629075978 (Linux, macOS) and a local Windows reference run on 2026-11-14 at `5a45202a`
  (`test1452: curl stopped after 60 s`).
- ADR-0080's 2026-11-11 amendment (BL-858) decided the server half plays the case in echo-and-close
  mode, and BL-1050 built `TelnetUpstreamTestCaseReplies(TimeSpan idlePeriod)` and the
  `TelnetProtocolServer(TelnetUpstreamTestCaseReplies)` constructor in
  `Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary`. Nobody composed it:
  `Conformance/Surl.Conformance.UnitTests/UpstreamTestCaseServerHalves.cs` still passes
  `new TelnetProtocolServer()` in its `playing` array.
- Compose the seam there, with an idle period well inside the suite's 60 s case timeout, and make sure
  the TELNET half's received bytes reach the arrival log if the seam records them (1452 has no
  `<protocol>` section, so this is for consistency, not for 1452).
- `Surl.Console` must keep composing the plain `TelnetProtocolServer()`; only the test project
  composes the seam (ADR-0080 decision 2).
- Stand-in: not applicable. This is the test project's composition; the shipped server is unchanged.

## Acceptance criteria

- [x] `UpstreamTestCaseServerHalves` constructs `TelnetProtocolServer` with a
      `TelnetUpstreamTestCaseReplies`.
- [x] A local Windows run of `UpstreamTestCasesAgainstSurlTests` writes `test1452 pass` and still
      passes every case on `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`.
- [x] `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean and the fast tests
      are green.

## Notes

- Composed with a 5 s idle period. Measured 1452 still failed on a trailing LF the verbatim echo returned; echo mode now withholds a final LF (ADR-0080 amendment 2026-11-14). Telnet library, its tests and the ADR added to touches (no Doing task names them).

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. TELNET echo-and-close half composed; test1452 passes on Windows
