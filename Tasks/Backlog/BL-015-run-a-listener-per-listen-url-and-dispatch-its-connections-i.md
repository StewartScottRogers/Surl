---
id: BL-015
title: Run a listener per listen URL and dispatch its connections in Surl.Core
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-004, BL-005]
touches: [Surl.Core.UnitLibrary, Surl.Core.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-015 — Run a listener per listen URL and dispatch its connections in Surl.Core

## Goal

`Surl.Core.UnitLibrary`'s serving engine starts one listener per listen URL through the
listener seam. It hands each accepted connection, with a fresh exchange context, to the
protocol server registered for the URL's scheme, and keeps serving when one exchange
fails. It stops cleanly on cancellation and returns the `SurlExitCode` the exit-code ADR
assigns for each outcome. All of this is proven with fakes and no socket.

## Context

- `Surl.Core.UnitLibrary/CLAUDE.md`: the serving engine. It references only
  `Surl.Protocol.Abstractions.UnitLibrary`, takes time from an injected `TimeProvider`,
  and gets transports through the seams.
- The listener-seam ADR recorded by BL-000 (types added by BL-005) defines the listener,
  connection, protocol-server, exchange-context and listen-URL types, and how bind
  failures are reported. The exit-code ADR recorded by BL-001 (members added by BL-004)
  gives the codes. Both are indexed in `Documentation/Planning/Decisions/README.md`.
- Protocol servers are registered by explicit construction, keyed by scheme, never by
  assembly scanning (native AOT; root `CLAUDE.md`).
- Tests use hand-written fakes: a fake listener that yields scripted connections, and a
  fake protocol server that records what it was given. Tests run in parallel at method
  level, so share no state. No `Thread.Sleep`: advance time with a hand-written
  `TimeProvider` subclass in `Surl.Core.UnitTests`. The `FakeTimeProvider` package is
  not approved, and no package may be added.

## Acceptance criteria

- [ ] A serving-engine type in `Surl.Core.UnitLibrary` is constructed with a listener
      factory, the protocol servers keyed by scheme, and a `TimeProvider`. Given listen
      URLs and a `CancellationToken`, it serves until cancelled and returns a
      `SurlExitCode`.
- [ ] Fast tests prove: one listener started per listen URL; each accepted connection
      goes to the server registered for its scheme, with an exchange context carrying
      that scheme and the connection's endpoints; two connections on one listener are
      served concurrently.
- [ ] Fast tests prove: a protocol server that throws ends only its own exchange, and
      the listener keeps accepting.
- [ ] Fast tests prove: a listener that fails to bind makes the engine stop every
      listener already started and return the bind-failure exit code the ADR assigns,
      and a listen URL whose scheme has no registered server returns the exit code the
      ADR assigns for it before any listener starts.
- [ ] Fast tests prove: cancellation stops accepting, waits for in-flight exchanges up to
      a shutdown timeout measured on the injected `TimeProvider`, then cancels them, and
      returns the exit code the ADR assigns for a normal stop.
- [ ] `dotnet build Surl.Core.UnitLibrary -warnaserror` is clean, the fast tests are
      green with no `Integration` test in `Surl.Core.UnitTests`, and
      `Measure-CodeQuality.ps1` reports no failing member in `Surl.Core.UnitLibrary`.

## Notes

Connection limits and idle timeouts are follow-up tasks once the command-line ADR gives
them options.

## Log

- 2026-09-28: Created.
