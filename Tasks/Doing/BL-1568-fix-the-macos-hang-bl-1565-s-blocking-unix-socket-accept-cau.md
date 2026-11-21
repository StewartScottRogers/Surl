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
completed:
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

- [ ] The Log names which suspect (or another cause) hangs, with evidence from a macOS run: a dispatched `ci` run on the
      shift branch with `--blame-hang-timeout` on `Surl.Console.UnitTests` (or the job's hang dump), parked as Blocked
      "Waiting for run:<id>".
- [ ] The accept is changed so disposal ends it promptly on every platform (e.g. `AcceptAsync` with the address
      problem avoided another way, `Shutdown` before `Dispose`, or a cancellable wait), and an `ArgumentException` for
      one peer can never loop on that same peer.
- [ ] A named test (Integration, real Unix socket, `OSCondition` not used to skip macOS) disposes a listener blocked in
      accept and asserts it ends within 5 s, and a test with a named-address peer still shows surl keeps serving.
- [ ] A dispatched `ci` run passes "Build and test (macos-latest)" and "Upstream test cases (macos-latest)"; its run id
      is in the Log.
- [ ] `dotnet build` clean, fast tests green, coverage gates held.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
