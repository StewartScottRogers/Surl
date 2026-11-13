---
id: BL-1103
title: Pin upstream curl's exit 7 when a dropped HTTP connection resets before its connect completes
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests/StandInFaultRecipeTests.cs, Documentation/Wiki]
requirement: none
created: 2026-11-13
completed:
---
# BL-1103 — Pin upstream curl's exit 7 when a dropped HTTP connection resets before its connect completes

## Goal

`StandInFaultRecipeTests.DropConnection_Once_FailsTheFirstCurlAndLetsTheRetrySucceed` passes
on CI's Windows, Linux and macOS jobs every run. It accepts every exit code pinned upstream
curl 8.21.0 gives on a connection Surl drops before any byte, and nothing else.

## Context

CI run 43418926863 (PR #29, frozen branch `factory/merge-20261113-043618`): the test failed
only on "Build and test (ubuntu-latest)", at `Assert.IsTrue(first.ExitCode is 55 or 56)`.
Upstream curl answered exit 7: `curl: (7) Failed to connect to 127.0.0.1:44189 after 8 ms:
Could not connect to server`. Windows and macOS passed. That holds the dark factory's merge
to `master`.

- Test: `Conformance/Surl.Conformance.UnitTests/StandInFaultRecipeTests.cs:76`. A `DropConnection`
  fault with `Times = 1` drops the first HTTP connection, then a retry must get `hello.txt`.
- The fault was applied: the test's `WaitForEventAsync(FaultApplied())` comes before the
  assertion and did not time out. So curl did reach the listener, and the reset arrived
  before curl's own connect check finished. That gives curl's "Could not connect" (7) rather
  than a send (55) or receive (56) failure.
- Root `CLAUDE.md`, "Tests pass on Windows, Linux and macOS": where upstream curl's answer
  differs by platform or timing, pin every answer it gives, never a looser one.

## Acceptance criteria

- [ ] The task's Log confirms the cause from the test's own events (a `FaultApplied` event exists for the first connection), or names the real one if it differs.
- [ ] The first fetch accepts exit 7, 55 or 56 and nothing else, with its stderr in the failure message, and the comment above the assertion names all three and cites CI run 43418926863.
- [ ] The retry still must exit 0 and return `hello\n`.
- [ ] Any document that lists the exit codes of a dropped connection names 7 too (search `Documentation/` for the `DropConnection` fault, e.g. the stand-in recipes page).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green on Windows.

## Notes

Filed by an interactive session on 2026-11-13 after PR #29's Linux job failed. Each of PRs #27,
#28 and #29 failed on a different timing-sensitive test, and this is the fourth such task
after BL-1100, BL-1101 and BL-1102.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing.
