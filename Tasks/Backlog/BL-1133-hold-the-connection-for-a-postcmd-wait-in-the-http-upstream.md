---
id: BL-1133
title: Hold the connection for a postcmd wait in the HTTP upstream test-case server half
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1133 — Hold the connection for a postcmd wait in the HTTP upstream test-case server half

## Goal

The HTTP upstream test-case server half implements a case's `<postcmd>` `wait <secs>`: after it has
sent a reply it pauses that long, on its injected `TimeProvider`, before it closes the connection or
reads the next request. Upstream tests 29, 160, 303 and 1074 then run instead of skipping
`servercmd-unsupported:postcmd`, and pass on Windows, Linux and macOS.

## Context

- All four skip on every platform (CI run 43629075978; local Windows reference run 2026-11-14).
- Upstream's definition, `docs/tests/FILEFORMAT.md` at tag `curl-8_21_0`, `<postcmd>`: "special
  purpose server-command to control its behavior *after* the reply is sent. For HTTP/HTTPS, these are
  supported: `wait [secs]` - Pause for the given time". No other `<postcmd>` word appears in a case at
  the tag. 29 (`wait 10`, `-m 2`, a `swsclose` reply without length) expects exit 28: the half must
  not close before curl's 2 s run out. 160 and 1074 (`wait 1`) expect curl to retry its second
  request on a new connection after the half closes the first. 303 is the HTTPS form of 29 (`wait
  20`).
- Where:
  - `Protocols/Http/Surl.Protocol.Http.UnitLibrary`: `HttpUpstreamTestCaseReplies` takes the
    `<postcmd>` lines beside the `<servercmd>` lines (a new constructor parameter or overload) and
    `HttpUpstreamTestCaseServer` waits after each reply. Use `TimeProvider`, never `Thread.Sleep`.
  - `Conformance/Surl.Conformance.UnitTests/UpstreamTestCasesAgainstSurlTests.cs`, where it constructs
    `HttpUpstreamTestCaseReplies` (about line 465): pass the case's `<reply><postcmd>` lines.
  - `Conformance/Surl.Conformance.UnitLibrary/UpstreamTestCaseServerCommands.cs`: the HTTP family
    implements `PostCommand`; fix the summary that says no server half implements it.
- Stand-in: not applicable. The seam is composed only by `Surl.Conformance.UnitTests`.

## Acceptance criteria

- [ ] A fast test with a fake `TimeProvider` shows the half sends a `swsclose` reply and keeps the
      connection open until the clock passes `wait 10`'s 10 s, then closes it.
- [ ] A fast test shows `UpstreamTestCaseSelector` no longer skips 29, 160, 303 and 1074 for
      `servercmd-unsupported:postcmd`.
- [ ] A local Windows run of `UpstreamTestCasesAgainstSurlTests` passes 29, 160, 303 and 1074 and
      every case already on `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`.
- [ ] `dotnet build -warnaserror` is clean for the four projects, the fast tests are green, and both
      libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
