---
id: BL-057
title: Keep accepting after a per-connection accept failure in Surl.Networking
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-011, BL-015]
touches: [Surl.Networking.UnitLibrary, Surl.Networking.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-057 — Keep accepting after a per-connection accept failure in Surl.Networking

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

- [x] A pure, fast-tested member of `Surl.Networking.UnitLibrary` classifies a
      `SocketError` from accept as per-connection (`ConnectionReset`,
      `ConnectionAborted`, and any other the implementer finds documented for accept) or
      listener-fatal (everything else). Fast tests cover both classes.
- [x] The TCP listener's `AcceptAsync` retries after a per-connection failure and throws
      for a listener-fatal one. Proven by a fast test through the classification seam,
      with no socket.
- [x] `dotnet build Surl.Networking.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Networking.UnitLibrary`.

## Notes

- Plan: a pure `AcceptFailureClassifier` (`IsPerConnection(SocketError)` and
  `IsPerConnection(Exception)`); `AcceptRace.AcceptNextAsync` loops, starting another
  accept on the source whose accept failed per-connection; a listener-fatal
  `SocketException` stays `IOException`. Decision recorded in ADR-0022.
- Per-connection errors: `ConnectionReset` (Windows accept/AcceptEx docs),
  `ConnectionAborted` (ECONNABORTED, accept(2)), and `HostDown`, `HostUnreachable`,
  `NetworkUnreachable` from Linux accept(2)'s pending-network-errors list. Left out
  `NetworkDown` and `OperationNotSupported`: Windows and BSD also use them for a dead or
  unusable listening socket, so retrying them could loop.
- Also covered: a client gone after `accept` but before `NoDelay` and the endpoints were
  read. `TcpConnectionListener` now does those inside the accept (`AcceptedSocket`),
  releases the socket and throws `AcceptedSocketLostException`, which is per-connection.
  Before, that path threw `IOException` from `AcceptAsync` and stopped the server too.
- Touches widened to `Documentation/Planning/Decisions` for ADR-0022 and its README row;
  no task in Doing names it.
- No integration test forces a real reset-before-accept: it is not deterministic across
  platforms, and the criterion asks for the fast test through the seam
  (`AcceptRaceTests.AcceptNextAsync_AcceptFailsForOneClient_*`).
- Gates: `dotnet build Surl.Networking.UnitTests -warnaserror` clean; Networking tests
  267/267 including Integration; `Measure-CodeQuality.ps1 -Library
  Surl.Networking.UnitLibrary` reports 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. A client that resets or aborts before it is accepted no longer stops the listener: AcceptRace absorbs per-connection accept failures (AcceptFailureClassifier, ADR-0022) and accepts again
