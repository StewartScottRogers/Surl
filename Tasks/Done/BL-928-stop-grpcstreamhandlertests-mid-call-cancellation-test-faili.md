---
id: BL-928
title: Stop GrpcStreamHandlerTests mid-call cancellation test failing on linux-arm64
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1035]
touches: [Surl.Extended.Protocol.Grpc.UnitTests, Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Http2.UnitLibrary]
requirement: none
created: 2026-11-06
completed: 2026-11-11
---
# BL-928 — Stop GrpcStreamHandlerTests mid-call cancellation test failing on linux-arm64

## Goal

`GrpcStreamHandlerTests.ServeStreamAsync_ClientClosesTheConnectionMidCall_WritesNothingAndLogsCancelled` passes on every run on every runner, linux-arm64 included.

## Context

- It failed in release.yml's `Publish linux-arm64` leg (fast tests) of run https://github.com/StewartScottRogers/Surl/actions/runs/42222500303 (master, `8eb0301b`, 25 ms) while passing on every other leg and on Windows locally, so it is timing-dependent: likely a race between the fake connection closing and the handler's write or log.
- Start at `Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests/GrpcStreamHandlerTests.cs`. Fix the race, not the timeout; time comes from `TimeProvider`.
- Found by BL-688.

## Acceptance criteria

- [x] The cause of the linux-arm64 failure is named in Notes, and the test or handler no longer depends on thread scheduling.
- [x] The test passes 200 times in a row locally (`dotnet test --filter` in a loop).

## Notes

- Cause: `Http2RequestBody.Abandon` (Surl.Http2.UnitLibrary) completed the body's channel *before* setting `HasEndedEarly`. Completing the channel wakes the gRPC handler's pending read on a thread-pool thread (`AllowSynchronousContinuations` is false). If that thread ran before the setter, `GrpcRequestReader` saw `HasClientLeft == false`, treated the closed connection as a clean half-close inside a partial message, and wrote trailers instead of logging CANCELLED. The window is wider on linux-arm64, where the plain auto-property write has weaker ordering.
- Fix (in the working tree, uncommitted; the shift stashes it): `End` and `Abandon` now go through one `Finish(endedEarly)` under a `Lock`. The first ending wins, `HasEndedEarly` is set before `TryComplete`, so the woken reader always sees it. The fix is not a timeout.
- `touches` now also names `Protocols/Http/Surl.Http2.UnitLibrary`, because the race lives there and not in the gRPC library. No task in Doing touched it (checked 2026-11-11).
- 2026-11-11: could not build or test. global.json pins SDK 10.0.401 and it is not installed on this machine (installed: 10.0.100-rc.1, 11.0.100-rc.2, 11.0.100). Returned to Backlog behind BL-1035. Next run: build, run fast tests, then loop the test 200 times.

- 2026-11-11: built with SDK 10.0.404 (rollForward). Http2 and gRPC tests pass, the mid-call test passed 200 of 200 runs, full fast suite green.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so the Http2RequestBody fix cannot be built or tested
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Fixed the Http2RequestBody ending race; 200/200 passes
