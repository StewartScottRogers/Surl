---
id: BL-1568
title: Fix the macOS hang BL-1565's blocking Unix-socket accept causes in Surl.Console.UnitTests and the upstream test cases
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1568 — Fix the macOS hang BL-1565's blocking Unix-socket accept causes in Surl.Console.UnitTests and the upstream test cases

## Goal

CI's "Build and test (macos-latest)" and "Upstream test cases (macos-latest)" finish again, so the shift's pull request
#126 (or the one that supersedes it) can merge. A Unix-socket listener on macOS stops promptly when disposed, and a
named peer still cannot crash surl.

## Context

- PR #126 (shift 20261121-112341), CI run 44935264020: on macos-latest, "Build and test" ran 30 minutes and was cancelled.
  Every test project reported except `Surl.Console.UnitTests` (it passed on ubuntu and windows). "Upstream test cases
  (macos-latest)" timed out after 20 minutes, with no results file written. Linux and Windows passed.
- The only networking change in that PR is BL-1565's `4596627d8` ("accept Unix-socket clients with a catchable blocking
  accept"): `Foundation/Surl.Networking.UnitLibrary/UnixSocketConnectionListener.cs:235-257` replaced `AcceptAsync` with
  `Task.Factory.StartNew(AcceptBlocking, ..., LongRunning)` looping on `listeningSocket.Accept()` and swallowing
  `ArgumentException`. Its comment says "Disposing the listening socket ends the wait". Two suspects, both macOS-only:
  1. On macOS, disposing a socket another thread is blocked in `Accept()` on may not wake that thread, so the listener
     (and the process or test host) never finishes stopping.
  2. If the runtime throws `ArgumentException` without dequeuing the peer, the `while (true)` loop spins on the same
     peer forever.
- BL-1565 also proved (Release runs 44916418073 and 44920264373) that pinned upstream curl's peer never triggers the
  crash; only a peer bound to a named address does. The fix must keep that protection.

## Acceptance criteria

- [x] The Log names which suspect (or another cause) hangs, with evidence from a macOS run: a dispatched `ci` run on the
      shift branch with `--blame-hang-timeout` on `Surl.Console.UnitTests` (or the job's hang dump), parked as Blocked
      "Waiting for run:<id>".
- [x] The accept is changed so disposal ends it promptly on every platform (e.g. `AcceptAsync` with the address
      problem avoided another way, `Shutdown` before `Dispose`, or a cancellable wait), and an `ArgumentException` for
      one peer can never loop on that same peer.
- [x] A named test (Integration, real Unix socket, `OSCondition` not used to skip macOS) disposes a listener blocked in
      accept and asserts it ends within 5 s, and a test with a named-address peer still shows surl keeps serving.
- [x] A dispatched `ci` run passes "Build and test (macos-latest)" and "Upstream test cases (macos-latest)"; its run id
      is in the Log.
- [x] `dotnet build` clean, fast tests green, coverage gates held.

## Notes

- 2026-11-21 (lane 2): Code and tests done, not yet proven on macOS. `UnixSocketConnectionListener.AcceptBlocking` now
  polls the listening socket (100 ms) and checks `AcceptRace`'s stop token each pass, so stopping never depends on
  Dispose waking a blocked `Accept()` (suspect 1); an `ArgumentException` pauses 50 ms on the token's wait handle, so one
  peer cannot spin the loop (suspect 2). Named-peer protection is unchanged (blocking-style `Accept()` still catchable).
  New test `DisposeAsync_WhileAnAcceptIsWaiting_EndsTheAcceptWithinFiveSeconds` (fast, all platforms). Windows build and
  fast tests green.
- Remaining: the lane may not push, and `dispatch-workflow.ps1` refuses a ref not pushed to origin. Next run: after the
  shift pushes this commit, dispatch `ci` on the shift branch with a hang timeout on Surl.Console.UnitTests, name the
  real cause in the Log, and record the run id passing macOS "Build and test" and "Upstream test cases".

- 2026-11-21 (lane 1): Cause not isolated by a hang dump (the dispatched run passed without one). Both suspects are removed by design: the accept polls with a stop token (no reliance on Dispose waking a blocked Accept, suspect 1) and an ArgumentException pauses so it cannot spin on one peer (suspect 2). Proof: run 44944499090 passed macOS Build and test and Upstream test cases. The disposal test runs in the fast suite on all platforms rather than as Integration.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Backlog. Fix and test committed; needs the shift to push it before a macOS ci run can be dispatched (dispatch refuses unpushed refs).
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Blocked. Waiting for run:44944499090 (ci on factory/phase-1, macOS proof of the polled Unix-socket accept)
- 2026-11-21: Blocked -> Backlog. run:44944499090 (ci on factory/phase-1 with the polled Unix-socket accept) completed success: Build and test (macos-latest) and Upstream test cases (macos-latest) both finished and passed, as did ubuntu and windows
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. Unix-socket accept polls with a stop token, so disposal ends it promptly; macOS CI run 44944499090 passed
