---
id: BL-1746
title: Stop the localhost loopback origin connector test timing out on a loaded machine
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1746 — Stop the localhost loopback origin connector test timing out on a loaded machine

## Goal

`SocketOriginConnectorLoopbackTests.ConnectAsync_LoopbackListener_CarriesBytesBothWays("localhost")` passes in a full
fast test run on a loaded Windows machine.

## Context

- Seen while working BL-1735 on 2026-11-25, on a dark factory lane with other lanes running: the `localhost` row failed
  after 10 s in one full run of `dotnet test --no-build --filter "TestCategory!=Integration"`. It passed in the two full
  runs before that, and a rerun of `Foundation/Surl.Networking.UnitTests` passed (560 passed, 0 failed, 2 skipped).
- Likely cause, read from the code and not proven: the test's `Timeout` is 10 s, the listener is on `127.0.0.1` only,
  and `localhost` may try `::1` first, which Windows takes seconds to refuse.
- The test is in `Foundation/Surl.Networking.UnitTests/SocketOriginConnectorLoopbackTests.cs`. Production timeouts are
  not to be changed to make it pass.
- Stand-in: test only; nothing a stand-in user sees changes.

## Acceptance criteria

- [x] Both rows of `ConnectAsync_LoopbackListener_CarriesBytesBothWays` pass, and neither can fail only because a
      refused `::1` attempt and a busy machine together used up the test's connect timeout.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Measured on this machine, 2026-11-25: `localhost` resolves to `::1` then `127.0.0.1`; a connect to a closed port is
  refused after 2047 ms on `::1` and 2032 ms on `127.0.0.1`. So the `localhost` row spends about 2 s of its connect
  timeout before reaching the listener. The 10 s failure itself was not reproduced here.
- Decision: the carrying test passes `Timeout.InfiniteTimeSpan` as the connect timeout (`NoConnectTimeout`). A larger
  finite value would still depend on machine load, and the operating system bounds each connect attempt. Listening on
  `::1` too was rejected: the test shows the connector falling back from `::1` to a `127.0.0.1`-only listener. Timeout
  behaviour stays covered by `SocketOriginConnectorTests` over fakes. No production code changed.
- `ConnectAsync_ClosedPort_IsRefused` keeps its 10 s timeout: it connects to `127.0.0.1` only (one refused attempt,
  about 2 s) and has not been seen to fail.
- Gates: `dotnet build -warnaserror` 0 warnings; `dotnet format --verify-no-changes` clean; fast tests 29388 passed,
  0 failed, 3 skipped; `Surl.Networking.UnitTests` integration tests 27 passed.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The localhost loopback origin connector test sets no connect timeout, so a slow refused ::1 attempt on a loaded machine can no longer time it out.
