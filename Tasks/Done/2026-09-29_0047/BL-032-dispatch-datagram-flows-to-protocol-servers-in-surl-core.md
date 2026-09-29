---
id: BL-032
title: Dispatch datagram flows to protocol servers in Surl.Core
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-015]
touches: [Surl.Core.UnitLibrary, Surl.Core.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-032 — Dispatch datagram flows to protocol servers in Surl.Core

## Goal

The serving engine from BL-015 starts a datagram listener for each listen URL whose
scheme is datagram-based (`tftp`), and hands each flow, with a fresh exchange context, to
the protocol server registered for that scheme. It uses the same failure isolation and
shutdown as TCP connections.

## Context

- The listener-seam ADR recorded by BL-000 (types added by BL-005) defines the
  datagram-flow and listener types, and how a protocol server declares that it takes
  datagram flows rather than connections. Its `README.md` index entry in
  `Documentation/Planning/Decisions/` names it.
- BL-015's engine, fakes and tests are the starting point. Extend its fakes with a fake
  datagram listener. No socket, and no `Thread.Sleep`.

## Acceptance criteria

- [x] For a `tftp` listen URL the engine asks the listener factory for a datagram
      listener, and hands each flow to the registered server with an exchange context
      carrying the scheme and endpoints. Proven by a fast test.
- [x] A throwing server ends only its own flow. Cancellation stops the datagram listener
      and in-flight flows as BL-015 does for connections. Both are proven by fast tests.
- [x] A mix of TCP and datagram listen URLs in one run starts both kinds. Proven by a
      fast test.
- [x] `dotnet build Surl.Core.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Core.UnitLibrary`.

## Notes

- Plan: split `ServingEngine` into a partial class. `ServingEngine.cs` keeps startup,
  shutdown and connections. `ServingEngine.Datagrams.cs` serves flows. Each started
  listener is a `StartedListener`, which pairs the listener with its own accept loop, so
  startup, stopping the listeners and the shared accept loop (`AcceptUntilStoppedAsync`)
  work the same for both kinds. Flows go through two internal decorators, mirroring the
  connection ones: `RecordingDatagramFlow` (logs each datagram) and
  `IdleClockRestartingDatagramFlow` (restarts the idle clock on each datagram).
- Choices, all within ADR-0004 and ADR-0006. No new ADR was needed; the Decisions folder
  is also BL-026's `touches`.
  - Which listener a URL gets depends on its server's type: `IDatagramProtocolServer` gets
    a datagram listener, `IConnectionProtocolServer` a connection listener. A scheme whose
    server is neither returns `UnsupportedProtocol` before any listener starts. A server
    that implements both is an `ArgumentException` at construction, because ADR-0004
    section 4 says "exactly one".
  - The first datagram reaches Surl before the server gets the flow. The engine logs it as
    `BytesReceived` when the exchange opens, so the log shows everything that crossed the
    transport (ADR-0004 section 5).
  - A flow has no `Abort`. When a server throws, the engine notes it and disposes the flow
    (ADR-0004 section 5). The end note says "closing the flow".
  - A flow counts as one connection, shares the connection counts and deadlines, and a
    flow past a limit goes to `IDatagramRefusalWriter` within `RefusalWriteDeadline`,
    then is disposed (ADR-0006). This was the smallest faithful path, so it lands here and
    also covers most of BL-075; see the note added there.
- Code review (code-reviewer agent): no bugs. Adopted two suggestions: reject a server
  that takes both transports, and fast tests for the per-address limit and the maximum
  duration on flows.
- `Measure-CodeQuality.ps1 -Library Surl.Core.UnitLibrary`: 100% line, 100% branch,
  0 failing members, worst CRAP 10. The script counts a lambda's complexity against the
  method beside it, so the two-transport check is a plain loop.
- Surl.Core.UnitTests: 96 tests (22 new).
- Filed BL-080: `Surl.Networking.UnitTests` has a certificate-chain test that began failing
  on every run late in this task. Networking does not reference Core, and nothing under
  `Surl.Networking.*` changed. Because of it, the last quality measurement used the run's
  coverage with `-SkipTestRun`.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Surl.Core serves tftp: a datagram listener per datagram-scheme URL, each flow to its server with a fresh context, under the same limits, failure isolation and shutdown as connections
