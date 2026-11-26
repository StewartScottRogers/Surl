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
completed:
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

- [ ] Both rows of `ConnectAsync_LoopbackListener_CarriesBytesBothWays` pass, and neither can fail only because a
      refused `::1` attempt and a busy machine together used up the test's connect timeout.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
