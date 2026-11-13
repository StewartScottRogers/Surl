---
id: BL-1105
title: Run CI's repeated test legs on factory/phase-1 and file a fix task for every flaky test
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-1104]
touches: [Tasks]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-1105 — Run CI's repeated test legs on factory/phase-1 and file a fix task for every flaky test

## Goal

Every test that fails some of the time on CI's Linux or macOS legs has its own fix task on the
board, found by one repeated CI run on `factory/phase-1`.

## Context

BL-1104 added a `workflow_dispatch` input to `.github/workflows/ci.yml` that runs the Linux and
macOS "Build and test" legs several times side by side. Its recipe is in
`Documentation/Wiki/Dark-Factory-Recipes.md`.

The flaky tests found so far each have a task already, or are fixed:

| Test | Task |
| --- | --- |
| `GetWithHttp3Only_Http3Off_FailsToConnect` | BL-1100 |
| `SeventeenthCheck_WaitsForASlot_InsideItsOwnTimeout` | BL-1101 |
| `ServeStreamAsync_ShutdownBeforeAnyReply_IsTrailersOnlyUnavailable` | BL-1102 |
| `DropConnection_Once_FailsTheFirstCurlAndLetsTheRetrySucceed` | BL-1103 |

One more is known and has no task yet:
`UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithHttp3_File_IsServedOverHttp3NotTheTcpFallback`
failed on macOS in CI run 43422998980 (PR #31). Its `-w` output byte at index 16 was `2`, not
`3`: curl fell back to HTTP/2 over TCP after about 10 s.

## Acceptance criteria

- [x] The task's Log names the run: `gh workflow run CI --ref factory/phase-1` with the attempts input set to 5, and its run URL and conclusion once every leg finished.
- [x] The Log lists every test that failed in any attempt, with OS, attempt and failure message, read from the run's `.trx` artifacts.
- [x] Each failing test without a task gets its own new `Backlog` task, filed through `task-planner`, `High` priority, assigned to `Claude`. Each names the test, the run, the attempts it failed in, the failure message, and a `touches` limited to its test file and the one library it tests. `GetWithHttp3_File_IsServedOverHttp3NotTheTcpFallback` gets one even if it passed every attempt.
- [x] A test that failed in this run and already has a task, from the table above, has a Log line added to that task, or to a new task if it is done (archived tasks are never edited), with this run's evidence.
- [x] If no attempt failed, the Log says so and the run URL proves it.

## Notes

Filed by an interactive session on 2026-11-13 at Stewart's request ("file the flake hunt").
`gh workflow run` and `gh run` are allowed in a lane; pushing is not, and is not needed. Wait
for the run with `gh run watch <id> --exit-status` or by polling `gh run view`, never with
`Thread.Sleep`. A run of 5 attempts takes about 30 to 60 minutes.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing.
- 2026-11-13: Ran `gh workflow run CI --ref factory/phase-1 -f attempts=5`: https://github.com/StewartScottRogers/Surl/actions/runs/43436178802 - concluded failure once every leg finished (Windows and upstream cases green; Ubuntu attempts 2 and 5 and macOS attempts 1 and 5 failed; the other 6 Unix legs passed).
- 2026-11-13: Failed tests, from the .trx artifacts: `GetWithHttp3_File_IsServedOverHttp3NotTheTcpFallback` on macOS attempts 1 and 5 and Ubuntu attempts 2 and 5 (`CollectionAssert.AreEqual failed. Element at index 16 do not match. Expected: 51 Actual: 50`); `ServeAsync_ClientFinishesTheConnectStreamWithinTheShutdownGrace_ClosesTheConnectionAtOnce` on Ubuntu attempt 2 (`The grace never passed, so no shutdown WT_CLOSE_SESSION. expected: "800078AE00" actual: ""`). None of the four tests from the Context table (BL-1100 to BL-1103) failed in this run.
- 2026-11-13: Filed BL-1106 (HTTP/3 fallback test) and BL-1107 (WebTransport shutdown grace test), both High, Claude.
- 2026-11-13: Doing -> Done. Repeated CI run 43436178802 found two flaky tests; BL-1106 and BL-1107 filed
