---
id: BL-1034
title: Fix flaky CI test ServeStreamAsync_StreamWhoseConnectWasRefused_IsResetWithBufferedStreamRejected on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1035]
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-1034 — Fix flaky CI test ServeStreamAsync_StreamWhoseConnectWasRefused_IsResetWithBufferedStreamRejected on Linux

## Goal

`ServeStreamAsync_StreamWhoseConnectWasRefused_IsResetWithBufferedStreamRejected` in
`Surl.Extended.Protocol.WebTransport.UnitTests` passes every time on Windows, Linux and macOS.

## Context

- CI run 42323185216 (pull request #24, frozen commit `0761588`, a documentation-only change) failed
  in `Build and test (ubuntu-latest)`, "Fast tests", on this one test after 1 s:
  `Assert.AreEqual(WebTransportHttp3Codes.BufferedStreamRejected, client.StopSendingCode(4))` -
  "Expected values to be equal". Windows and macOS passed the same commit, and nothing in the WebTransport
  libraries changed, so the test (or the code it drives) depends on timing.
- Likely shapes: the assertion reads the client's STOP_SENDING code before the server has written it (a
  race the test should await on, not read once), or the server resets the stream in an order that varies.
  Find which with the test running in a loop on Linux or under load, then make it deterministic - await the
  frame through the fake connection, or inject the ordering - never a sleep (root `CLAUDE.md`: no
  `Thread.Sleep`, inject `TimeProvider`).

## Acceptance criteria

- [ ] The cause is named in Notes, with the evidence (a loop of the test, or the race traced in the code).
- [ ] The test passes 200 times in a row locally (`dotnet test --filter "FullyQualifiedName~ServeStreamAsync_StreamWhoseConnectWasRefused"`
      in a loop), and the fix waits on an event rather than on time.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the WebTransport library keeps 100%
      line and branch coverage.

## Notes

- Lane 4, 2026-11-11: lane 1's fix (commit a4695537 on factory/BL-1034-lane-1-20261111-060243) waits for both RESET_STREAM and STOP_SENDING in the two `WebTransportSessionDirectoryTests` refused-stream tests, because `WebTransportSessionDirectory.RefuseAsync` sends RESET_STREAM before STOP_SENDING and the test read STOP_SENDING once after seeing the reset. Its integration "build failed" was not the fix: `global.json` pins SDK 10.0.401 and no 10.0.4xx SDK is installed (`dotnet --list-sdks`: 10.0.100-rc.1, 11.0.100-rc.2, 11.0.100), so no build can run. The fix is reapplied uncommitted for the shift to stash; parked behind BL-1035.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 1 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-1034-lane-1-20261111-060243; start with git cherry-pick --no-commit factory/BL-1034-lane-1-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds; the test fix is ready and reapplied uncommitted.
