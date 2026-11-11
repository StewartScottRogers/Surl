---
id: BL-1034
title: Fix flaky CI test ServeStreamAsync_StreamWhoseConnectWasRefused_IsResetWithBufferedStreamRejected on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-11
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

- [x] The cause is named in Notes, with the evidence (a loop of the test, or the race traced in the code).
- [x] The test passes 200 times in a row locally (`dotnet test --filter "FullyQualifiedName~ServeStreamAsync_StreamWhoseConnectWasRefused"`
      in a loop), and the fix waits on an event rather than on time.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the WebTransport library keeps 100%
      line and branch coverage.

## Notes

- **Cause (race traced in the code).** `WebTransportSessionDirectory.RefuseAsync` sends two frames
  in order: first `stream.ResetAsync(code)` (RESET_STREAM), then `stream.StopSendingAsync(code)`
  (STOP_SENDING). The test waited only for `client.ResetCode(4) is not null`, then read
  `client.StopSendingCode(4)` once. When the test thread wakes between the two frames, STOP_SENDING
  has not arrived yet and the code is still null. That is the "Expected values to be equal" failure
  on the ubuntu runner. The server's ordering is correct; the test was wrong.
- **Fix.** The test's `UntilAsync` now waits for `ResetCode(4) is not null && StopSendingCode(4) is not null`.
  That is an event wait, not a timer, and it matches the pattern the other tests in this project
  already use (`WebTransportConnectHandlerTests` line 122). The sibling test
  `ServeStreamAsync_StreamNamingAnEndedSession_IsResetWithSessionGone` had the same race on its
  `SessionGone` STOP_SENDING assertion and got the same fix. No production code changed.
- **Evidence.** 200 runs in a row of the filtered test passed with 0 failures (2026-11-11, Windows).
  Full `dotnet build -warnaserror` is clean and the fast tests are green. Coverage collector
  (`--collect:"Code Coverage;Format=cobertura"`) reports `Surl.Extended.Protocol.WebTransport.UnitLibrary`
  at line 1.0 and branch 1.0.
- **Machine note.** This lane's machine has no 10.0.4xx SDK: only 10.0.100-rc.1, 11.0.100-rc.2 and
  11.0.100 are installed, so `global.json` (10.0.401, `latestFeature`) cannot resolve. To verify, I set
  `rollForward` to `latestMajor` temporarily (SDK 11.0.100) and restored the file afterwards, byte for
  byte. It was not committed. Someone needs to reinstall SDK 10.0.4xx on this machine, or other lanes'
  builds will fail the same way.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. The WebTransport refused-stream tests wait for STOP_SENDING as well as RESET_STREAM, so they no longer race on Linux
