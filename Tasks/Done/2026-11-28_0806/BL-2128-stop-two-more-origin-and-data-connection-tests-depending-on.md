---
id: BL-2128
title: Stop two more origin and data connection tests depending on load or a released port
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2128 — Stop two more origin and data connection tests depending on load or a released port

## Goal

`ActiveDataConnection_NobodyListening_IsUnreachable` and `ConnectAsync_UnresolvableName_IsHostNotFound` cannot fail
only because the machine is loaded or another socket took a released port.

## Context

Found while working BL-2124, by reading the code; neither test has been seen to fail. Both are in
`Foundation/Surl.Networking.UnitTests`:

- `SocketDataConnectionOpenerIntegrationTests.ActiveDataConnection_NobodyListening_IsUnreachable` (`Integration`)
  releases a probe port before connecting and passes a 10 s timeout, the two faults BL-2124 removed from
  `SocketOriginConnectorLoopbackTests.ConnectAsync_ClosedPort_IsRefused`. BL-2124's fix (hold the port as the local
  end of an open connection) applies as it stands.
- `SocketOriginConnectorLoopbackTests.ConnectAsync_UnresolvableName_IsHostNotFound` passes a 10 s timeout that covers
  the system resolver, so a slow lookup is reported as `TimedOut`.

## Acceptance criteria

- [x] `ActiveDataConnection_NobodyListening_IsUnreachable` holds its port until the connect has failed, sets no
      connect timeout, and still asserts `DataConnectionFailure.Unreachable`.
- [x] `ConnectAsync_UnresolvableName_IsHostNotFound` sets no connect timeout and still asserts
      `OriginConnectionFailure.HostNotFound`.
- [x] `dotnet test Foundation/Surl.Networking.UnitTests` passes with 0 failed.

## Notes

- The data connection test now holds its target port as the local end of an open loopback connection, BL-2124's
  method, and passes `Timeout.InfiniteTimeSpan`; the operating system still bounds the connect.
- The name test passes `NoConnectTimeout`; that field's comment now says the timeout covers the name lookup too.
- No failing test first: neither fault can be reproduced on demand (it needs a loaded machine or a taken port).
- 2026-11-28 results: `dotnet build -warnaserror` 0 warnings; `dotnet format --verify-no-changes` clean;
  `Surl.Networking.UnitTests` (Integration included) 589 passed, 0 failed, 2 skipped; solution fast tests
  30877 passed, 0 failed, 3 skipped.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. Both networking tests set no connect timeout, and the active data connection test holds its port until the connect has failed.
