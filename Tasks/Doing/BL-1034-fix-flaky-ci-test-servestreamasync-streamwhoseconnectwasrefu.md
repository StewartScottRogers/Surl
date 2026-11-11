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

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
