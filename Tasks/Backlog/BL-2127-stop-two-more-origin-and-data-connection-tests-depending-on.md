---
id: BL-2127
title: Stop two more origin and data connection tests depending on load or a released port
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-28
completed:
---
# BL-2127 — Stop two more origin and data connection tests depending on load or a released port

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

- [ ] `ActiveDataConnection_NobodyListening_IsUnreachable` holds its port until the connect has failed, sets no
      connect timeout, and still asserts `DataConnectionFailure.Unreachable`.
- [ ] `ConnectAsync_UnresolvableName_IsHostNotFound` sets no connect timeout and still asserts
      `OriginConnectionFailure.HostNotFound`.
- [ ] `dotnet test Foundation/Surl.Networking.UnitTests` passes with 0 failed.

## Notes

## Log

- 2026-11-28: Created.
