---
id: BL-046
title: Keep accepting after a per-connection accept failure in Surl.Networking
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-011, BL-015]
touches: [Surl.Networking.UnitLibrary, Surl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-046 — Keep accepting after a per-connection accept failure in Surl.Networking

## Goal

`Surl.Networking`'s connection listener absorbs an accept failure that belongs to one
client (the client reset or aborted the connection before it was accepted) and goes on
accepting, so `IConnectionListener.AcceptAsync` throws only for a failure of the listener
itself or for cancellation.

## Context

- BL-015's serving engine (`Surl.Core.UnitLibrary/ServingEngine.cs`,
  `AcceptUntilStoppedAsync`) treats any exception from `AcceptAsync` other than its own
  cancellation as a failure of the whole listener: it stops every listener, drains the
  exchanges, and rethrows, which `Surl.Console` turns into `InternalError` (125, ADR-0005).
  That is right for a listener that is broken, and wrong for one client's reset.
- Found by the code review of BL-015: on Windows a client that resets before `accept`
  returns surfaces as `SocketException` with `SocketError.ConnectionReset`; on Linux as
  `ECONNABORTED` (`SocketError.ConnectionAborted`).
- ADR-0004 section 6 defines the listener contract; section 8 says how Networking meets
  the coverage gate (the decision is pure and fast-tested, the socket call is excluded).

## Acceptance criteria

- [ ] A pure, fast-tested member of `Surl.Networking.UnitLibrary` classifies a
      `SocketError` from accept as per-connection (`ConnectionReset`,
      `ConnectionAborted`, and any other the implementer finds documented for accept) or
      listener-fatal (everything else). Fast tests cover both classes.
- [ ] The TCP listener's `AcceptAsync` retries after a per-connection failure and throws
      for a listener-fatal one. Proven by a fast test through the classification seam,
      with no socket.
- [ ] `dotnet build Surl.Networking.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
