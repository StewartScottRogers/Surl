---
id: BL-848
title: Time TCP and Unix socket connections' peeks and closes by the instance's TimeProvider
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822]
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests, Application/Surl.Api, Application/Surl.Api.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-848 — Time TCP and Unix socket connections' peeks and closes by the instance's TimeProvider

## Goal

Every TCP and Unix socket connection an instance accepts times its peek waits and its graceful and lingering
close by that instance's `TimeProvider`, not the system clock.

## Context

- Decision: ADR-0124 decision 6 - every time-dependent path uses the instance's injected `TimeProvider`. Found by
  BL-786's audit.
- `Foundation/Surl.Networking.UnitLibrary/StreamConnection.cs:78` defaults to `TimeProvider.System` and uses it
  at lines 136 (peek wait), 339 (graceful close) and 356 (lingering close).
  `TcpConnectionListener.CreateConnection` (line 164) and `UnixSocketConnectionListener.AcceptAsync` (line 110)
  never pass one, and `SocketListenerFactory` has none to pass.
- `SocketListenerFactory` is constructed in `Application/Surl.Api/SurlServerHostSeams.cs:44` and
  `Application/Surl.Console/Program.cs:61`; both hosts already hold the instance's `TimeProvider`.
- `SocketDataConnectionOpener` and `SocketOriginConnector` also default a null `TimeProvider` to the system clock;
  the composition passes one to the opener (`ComposedServing.cs:112`) - check the connector's callers and pass
  one there too if a host constructs it.
- Fix: `SocketListenerFactory` takes a `TimeProvider` and hands it through both listeners to `StreamConnection`;
  the hosts pass theirs.

## Acceptance criteria

- [ ] `TcpConnectionListener` and `UnixSocketConnectionListener` construct every `StreamConnection` with the
      factory's `TimeProvider`; a Networking test shows a connection's peek wait ends when a fake `TimeProvider`
      is advanced, not by wall time.
- [ ] `Surl.Api` and `Surl.Console` pass their `TimeProvider` to `SocketListenerFactory`.
- [ ] The touched libraries stay at 100% line and branch coverage; `dotnet build` clean, fast tests green on
      Windows, with no platform-specific assertion outside an `[OSCondition]` test.

## Notes

## Log

- 2026-11-05: Created.
