---
id: BL-2120
title: Make the closed-port origin connector test pass reliably
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-28
completed:
---
# BL-2120 — Make the closed-port origin connector test pass reliably

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

- [ ] The task's Notes name the cause, with the failure reproduced or the code path that allows it shown.
- [ ] The test no longer depends on that cause, and still asserts `OriginConnectionFailure.Refused`.
- [ ] `dotnet test Foundation/Surl.Networking.UnitTests` passes five runs in a row.

## Notes

## Log

- 2026-11-28: Created.
