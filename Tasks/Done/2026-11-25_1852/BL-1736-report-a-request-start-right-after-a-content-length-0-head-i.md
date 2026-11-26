---
id: BL-1736
title: Report a request start right after a Content-Length: 0 head in HttpRequestHeadBytesRedactor
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.HttpMessage.UnitLibrary, Protocols/Http/Surl.HttpMessage.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1736 — Report a request start right after a Content-Length: 0 head in HttpRequestHeadBytesRedactor

## Goal

`HttpRequestHeadBytesRedactor.IsAtRequestStart` is true immediately after a head that declares `Content-Length: 0`.

## Context

- Found while working BL-1713. In `Protocols/Http/Surl.HttpMessage.UnitLibrary/HttpRequestHeadBytesRedactor.cs`,
  `StartBody` enters the `Content-Length` body state with nothing remaining and leaves it only on the next byte, so the
  property is false between the head and that byte.
- The HTTP proxy's `HttpProxyConnectionBytesRedactor` asks the property before it hands bytes on, so it takes an `h2c`
  preface that follows such a head as misplaced and withholds every later byte.
- `RtspRequestBytesRedactor` asks it too, before reading `$` as an interleaved frame (found by reading the code; no
  test reproduces it). The fix covers both without changing either project.
- Tests: `Protocols/Http/Surl.HttpMessage.UnitTests/HttpRequestHeadBytesRedactorTests.cs`. A bug fix starts with the
  failing test.
- Stand-in: this changes only where the redactor sees a request boundary; no store, event or fault is involved.

## Acceptance criteria

- [x] `HttpRequestHeadBytesRedactorTests.IsAtRequestStart_RightAfterAHeadDeclaringContentLengthZero_IsTrue` passes: the
      property is true after exactly the head's bytes, with no further byte given.
- [x] `HttpRequestHeadBytesRedactorTests.Redact_NextHeadAfterAContentLengthZeroHead_MasksItsAuthorization` passes.
- [x] The tests `HttpRequestHeadBytesRedactorTests` held before this task pass unchanged.
- [x] Build with `-warnaserror` is clean, fast tests are green, and `Surl.HttpMessage.UnitLibrary` stays at 100% line
      and branch coverage.

## Notes

- **The reported bug does not exist; no production code changed.** The Context above is wrong:
  `HttpRequestBodyFraming.Of` reads `Content-Length: 0` as `HttpRequestBodyFramingKind.None`, so `StartBody` goes
  straight back to the head state and never enters the `Content-Length` body state with nothing remaining. Both new
  tests passed against the unchanged library, so there was no failing test to start from.
- The first new test was checked against the described behaviour: with the zero-length case temporarily made
  `ContentLength`, it failed (`IsAtRequestStart` false); the line was then restored. Both tests stay as regression
  guards.
- The proxy and RTSP symptoms follow from the same claim, so neither occurs (by reading
  `HttpProxyConnectionBytesRedactor` and `RtspRequestBytesRedactor`: both ask the property, which is true there; no
  test was added in those projects).
- ADR-0182 Amendment 8 still lists the non-existent bug as an accepted limit. It is outside `touches` (BL-1710, in
  Doing, names it), so it is filed as BL-1739.
- Pipeline: a test-only diff, so the plan, implement, review and conformance stages had nothing to act on and were
  not delegated.
- Evidence: `dotnet build -warnaserror` clean (0 warnings), `dotnet format --verify-no-changes` clean, fast tests
  all passed with 0 failures; `Measure-CodeQuality.ps1 -Library Surl.HttpMessage.UnitLibrary`: 100% line, 100%
  branch, 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. IsAtRequestStart is already true right after a Content-Length: 0 head; two regression tests pin it, no production change, ADR correction filed as BL-1739
