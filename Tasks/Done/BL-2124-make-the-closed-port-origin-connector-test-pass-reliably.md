---
id: BL-2124
title: Make the closed-port origin connector test pass reliably
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2124 — Make the closed-port origin connector test pass reliably

## Goal

`ConnectAsync_ClosedPort_IsRefused` passes however loaded the machine is.

## Context

Seen while working BL-2116 on 2026-11-28: the test failed once in a full fast-test run (`Surl.Networking.UnitTests`,
561 passed, 1 failed, 2 skipped), passed when rerun alone, and passed in the next full run. The change under test
touched only mail-store comments. The failure message was not kept.

The test is in `Foundation/Surl.Networking.UnitTests/SocketOriginConnectorLoopbackTests.cs`. Two possible causes,
neither confirmed: its 10 s connect timeout expiring under load (BL-1746 found loopback connects far slower on a
loaded machine, and Windows takes about 2 s to refuse a closed port), or another process taking the port
`ClosedLoopbackPort` released before the connect.

## Acceptance criteria

- [x] The task's Notes name the cause, with the failure reproduced or the code path that allows it shown.
- [x] The test no longer depends on that cause, and still asserts `OriginConnectionFailure.Refused`.
- [x] `dotnet test Foundation/Surl.Networking.UnitTests` passes five runs in a row.

## Notes

- Cause: the test had two ways to fail under load, and which one fired on 2026-11-28 is unknown (the failure message
  was not kept). Both were shown on this machine on 2026-11-28 with a PowerShell script over .NET sockets, and both are
  removed.
  - Timeout: Windows refused a closed `127.0.0.1` port after 2031 to 2063 ms. `SocketOriginConnector.ConnectAsync`
    turns an expired connect timeout into `OriginConnectionFailure.TimedOut` (its `catch (OperationCanceledException)
    when (timeoutSource.IsCancellationRequested ...)` filter), so a refusal slower than the test's 10 s fails the
    assertion. Reproduced at the socket with a 1 s limit: the connect was cancelled before the refusal arrived.
  - Released port: after the probe listener stopped, a second listener bound the same port and the connect succeeded,
    so the test would see no exception. Less likely of the two (inference: Windows handed out successive port-0 binds
    upwards in these runs).
- Fix: the port is the local end of an open loopback connection held until the assertion ends, and the connect passes
  `NoConnectTimeout`, as BL-1746 did for the carrying test; the operating system still bounds the attempt. Measured:
  a connect to such a port was refused after 2046 ms, and a second loopback listener on it failed with
  `AddressAlreadyInUse`.
- Rejected: a socket bound and never listening. On Windows it still let a wildcard listener bind the port, and whether
  macOS refuses a connect to such a socket was not verified (CI runs this test there). A port held by a connected
  socket has no listening socket on any platform.
- No production code changed. Linux and macOS behaviour is checked by CI only.
- Follow-up filed: BL-2127 (two other tests in the project with the same kind of dependence, never seen to fail).
- Gates: `dotnet build -warnaserror` 0 warnings; `dotnet format --verify-no-changes` clean; fast tests 30870 passed,
  0 failed, 3 skipped; `Surl.Networking.UnitTests` run five times in a row, 589 passed, 0 failed, 2 skipped each
  (integration tests included, as the command has no filter); its integration tests alone 27 passed.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The closed-port origin connector test holds its port and sets no connect timeout, so load and port reuse cannot fail it.
