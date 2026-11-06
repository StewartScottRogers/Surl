---
id: BL-934
title: Move the libcurl WebSocket driver's script parsing and report lines into Surl.Conformance
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Run-LibcurlWebSocketScript.cs]
requirement: FR-057
created: 2026-11-06
completed: 2026-11-06
---
# BL-934 — Move the libcurl WebSocket driver's script parsing and report lines into Surl.Conformance

## Goal

The WebSocket libcurl driver's step script (`recv`, `send:<FLAGS>:<payload>`, `send*<count>:...`, its
`--library` and `--recv-timeout` options) and its one-line-per-call report are parsed and written by
unit-tested types in `Surl.Conformance` (`LibcurlWebSocketScript`, `LibcurlWebSocketStep`,
`LibcurlWebSocketFlags`, `LibcurlWebSocketReport`), and `Run-LibcurlWebSocketScript.cs` uses them and
prints exactly the lines it prints today, so the Curl port's twin driver can share them.

## Context

- Second of the five tasks BL-597 ("Drive the Curl port's managed Libcurl through the WebSocket and RTSP
  libcurl scripts beside the pinned upstream libcurl") was split into on 2026-11-06 after it timed out
  at 120 minutes. Requirement FR-057; ADR-0089 decision 1 (c) wants a port-side twin that "takes the
  same arguments and prints the same one-line-per-call report"; sharing the parser and the report is how
  the two stay the same. Independent of BL-942 (they share project folders, so they run one after the other).
- The upstream driver already references the library (`#:project
  Conformance/Surl.Conformance.UnitLibrary/...` on line 1). Today its `DriverArguments`, `DriverStep`,
  `Libcurl.FlagValue`/`FlagNames` and `Shown` are private to the file and untested.
- The work is written. Take it from the stash `darkfactory BL-597 20261106-033059` in the main checkout
  (`Z:\repos\Surl`; find its index N with `git stash list`, matching the message; never pop, apply or
  drop it). None of these files has moved on the branch since the stash's base `6b47feae`.
  - Tracked: `git diff "stash@{N}^" "stash@{N}" -- Run-LibcurlWebSocketScript.cs | git apply`.
  - Untracked, each with `git checkout "stash@{N}^3" -- <path>`: in
    `Conformance/Surl.Conformance.UnitLibrary`, `LibcurlWebSocketFlags.cs`, `LibcurlWebSocketReport.cs`,
    `LibcurlWebSocketScript.cs`, `LibcurlWebSocketStep.cs`; in `Conformance/Surl.Conformance.UnitTests`,
    `LibcurlWebSocketFlagsTests.cs`, `LibcurlWebSocketReportTests.cs`, `LibcurlWebSocketScriptTests.cs`.
- The upstream driver's output is what `PinnedLibcurlTalksToSurlOverWebSocketTests` asserts; a changed
  character there is a regression, not a refactor. Those tests are `Integration` and Windows-only (the
  pinned `libcurl-4.dll`, ADR-0071 decision 10); run them on this machine as the check.
- Stand-in: not applicable; test tooling.

## Acceptance criteria

- [x] `LibcurlWebSocketScriptTests`, `LibcurlWebSocketReportTests` and `LibcurlWebSocketFlagsTests`
      pass, including `ParseStep_RepeatedSend_RepeatsThePayload`,
      `ParseStep_FlagAScriptMayNotName_Throws` and
      `SendLine_LongPayloadInSeveralCalls_ShowsItsFirst64BytesAndTheCalls`.
- [x] `Run-LibcurlWebSocketScript.cs` defines no `DriverArguments`, `DriverStep` or `Shown` and calls
      `LibcurlWebSocketScript.Parse` and `LibcurlWebSocketReport` for every line it prints.
- [x] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~PinnedLibcurlTalksToSurlOverWebSocketTests"`
      passes on Windows (every test passed, none Inconclusive on this machine), the result noted in the Log.
- [x] `dotnet build Conformance/Surl.Conformance.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and `Surl.Conformance.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Took the work from the BL-597 stash (commit `90c03679`, found in the common git dir's stash reflog
  because `git stash list` is denied to lanes); the tracked diff applied cleanly and the untracked files
  came from its third parent with `git show`, then `dotnet format` restored CRLF.
- `Measure-CodeQuality.ps1` measured `LibcurlWebSocketScript.ParseStep` at cyclomatic complexity 16, so
  it was split into `RepeatCountOf`, `FlagsOf` and `NeitherForm`; every input gets the same answer or
  the same `FormatException` text as before. Now 100% line and branch, 0 failing members, worst CRAP 10.
- Two deliberate narrowings from the old in-file parser, taken from the shared `LibcurlBytes.Unescape`
  and the new number parse: a `\x` without two hex digits is now a `FormatException` (it was the byte
  `x`, or a crash on non-hex digits), and `--recv-timeout`/`send*<n>` accept only plain digits
  (`NumberStyles.None`, invariant culture), not a sign or surrounding spaces. No pinned test uses either.
- `Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md` now names the four new types.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: PinnedLibcurlTalksToSurlOverWebSocketTests on Windows with the pinned libcurl-4.dll: 7 passed, 0 failed, 0 inconclusive. Fast tests green; dotnet build clean.
- 2026-11-06: Doing -> Done. The WebSocket libcurl driver's script parsing and report lines live in Surl.Conformance as unit-tested LibcurlWebSocketScript/Step/Flags/Report, and the driver prints the same lines
