---
id: BL-046
title: Implement IListenerFactory in Surl.Networking over the TCP and UDP listeners
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-011, BL-031]
touches: [Surl.Networking.UnitLibrary, Surl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-046 — Implement IListenerFactory in Surl.Networking over the TCP and UDP listeners

## Goal

`Surl.Networking.UnitLibrary` has a public class implementing `IListenerFactory`, so
`Surl.Console`'s composition root can hand the serving engine (BL-015) one object that
starts both kinds of listener.

## Context

- ADR-0004, section 6: "`Surl.Networking` implements `IListenerFactory` (BL-011 for
  connections, BL-031 for flows)". BL-011 delivered `TcpConnectionListener.StartAsync`
  but no factory, because a factory whose datagram half throws until BL-031 lands would
  be a type that does not do what its interface says. See BL-011's Notes.
- `StartConnectionListenerAsync` delegates to `TcpConnectionListener.StartAsync`;
  `StartDatagramListenerAsync` to the datagram listener BL-031 adds.
- The factory holds no state, so it needs no socket to be constructed; each method is a
  one-line delegation. Cover the delegation with integration tests on `127.0.0.1:0`, or
  exclude it as ADR-0004 section 8 allows, with a justifying comment.

## Acceptance criteria

- [ ] A public sealed class in `Surl.Networking.UnitLibrary` implements `IListenerFactory`.
- [ ] An `[TestCategory("Integration")]` test starts a connection listener through the
      factory on `127.0.0.1:0` and accepts one connection from a `TcpClient`.
- [ ] An `[TestCategory("Integration")]` test starts a datagram listener through the
      factory on `127.0.0.1:0` and receives one datagram from a `UdpClient`.
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Networking.UnitLibrary` reports no failing
      member; `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Filed by BL-011 (code review, 2026-09-28).

## Log

- 2026-09-28: Created.
