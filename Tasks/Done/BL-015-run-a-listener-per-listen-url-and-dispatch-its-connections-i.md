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
completed: 2026-09-28
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

- [x] A serving-engine type in `Surl.Core.UnitLibrary` is constructed with a listener
      factory, the protocol servers keyed by scheme, and a `TimeProvider`. Given listen
      URLs and a `CancellationToken`, it serves until cancelled and returns a
      `SurlExitCode`.
- [x] Fast tests prove: one listener started per listen URL; each accepted connection
      goes to the server registered for its scheme, with an exchange context carrying
      that scheme and the connection's endpoints; two connections on one listener are
      served concurrently.
- [x] Fast tests prove: a protocol server that throws ends only its own exchange, and
      the listener keeps accepting.
- [x] Fast tests prove: a listener that fails to bind makes the engine stop every
      listener already started and return the bind-failure exit code the ADR assigns,
      and a listen URL whose scheme has no registered server returns the exit code the
      ADR assigns for it before any listener starts.
- [x] Fast tests prove: cancellation stops accepting, waits for in-flight exchanges up to
      a shutdown timeout measured on the injected `TimeProvider`, then cancels them, and
      returns the exit code the ADR assigns for a normal stop.
- [x] `dotnet build Surl.Core.UnitLibrary -warnaserror` is clean, the fast tests are
      green with no `Integration` test in `Surl.Core.UnitTests`, and
      `Measure-CodeQuality.ps1` reports no failing member in `Surl.Core.UnitLibrary`.

## Notes

Connection limits and idle timeouts are follow-up tasks once the command-line ADR gives
them options.

What was built (2026-09-28):

- `ServingEngine` (public): constructed with an `IListenerFactory`, the protocol servers as
  an `IReadOnlyList<IProtocolServer>` (keyed by scheme into an ordinal table; two servers
  listing one scheme throw `ArgumentException`, ADR-0004 section 4), an
  `IExchangeLogFactory`, a `TimeProvider` and a shutdown grace period.
  `ServeAsync(listenUrls, cancellationToken)` returns the `SurlExitCode`.
- `RecordingConnection` (internal): the decorator ADR-0004 section 5 asks for; every byte
  read or written is reported to the exchange log before the server sees it.
- `InFlightExchanges` (internal): a count of running exchanges rather than a task per
  exchange, so a long-running server holds nothing per finished exchange.
- Tests: 33 fast tests in `Surl.Core.UnitTests` with hand-written fakes
  (`FakeListenerFactory`, `FakeConnectionListener`, `FakeConnectionProtocolServer`,
  `FakeExchangeLogFactory`, `ManualTimeProvider`). No socket, no `Thread.Sleep`, no
  `Integration` test. `Measure-CodeQuality.ps1`: 100% line, 100% branch, worst CRAP 8.

Choices taken under the delegation (sensible defaults, recorded here rather than in an
ADR because `Documentation/Planning/Decisions` is outside this task's `touches` and BL-024
holds it):

- **The engine takes an `IExchangeLogFactory` too.** `ExchangeContext` needs a log, and
  ADR-0004 section 5 says the engine calls `Create` once per exchange.
- **Shutdown grace period is a constructor argument**, with
  `ServingEngine.DefaultShutdownGracePeriod` = 5 seconds for a caller with no reason to
  choose. BL-024 / the command-line ADR may give it an option later; `Surl.Console`
  (BL-019) passes the default until then.
- **Exchange IDs count from 1 per engine.** `surl` builds one engine per process, so this
  is ADR-0004's "unique for the life of the process" without static state, which would
  break parallel tests.
- **A datagram server's scheme is refused as `UnsupportedProtocol`** before any listener
  starts. BL-032 replaces this with datagram dispatch.
- **Cancellation during startup returns `Ok`** after stopping the listeners already
  started (ADR-0005 section 2: a signal before serving starts also returns 0).
- **An unexpected failure** (a start failure that is neither a bind failure nor the
  caller's cancellation, an accept failure, or a listener that fails to stop) stops every
  listener, drains the exchanges, and is rethrown with its stack trace. `Surl.Console`
  maps it to `InternalError` (ADR-0005, "one mapping, in one place"). A listener that fails
  to stop after a bind failure does not hide the bind exit code.
- **An exchange that fails is aborted and still disposed**, whether the server threw or
  the exchange log did (found in review). A server's `OperationCanceledException` counts
  as a normal end only when the engine's give-up token was cancelled; otherwise it is a
  failure and the connection is aborted.
- **After the grace period the engine cancels the exchanges and waits for them** without
  a second deadline. A server that ignores its token holds shutdown up; a second Ctrl+C
  ends the process. Revisit with BL-025's limits if that proves wrong.

Learned: an `await` inside `catch` or `finally`, followed by `throw;`, makes the compiler
emit branches no test can reach, which fails the 100% branch gate. The engine captures a
failure as an `ExceptionDispatchInfo` (`CaptureFailureAsync`), cleans up outside any
handler, then rethrows.

Follow-up filed: BL-046 (the TCP listener must keep accepting after one client resets
before accept; today the engine rightly treats any accept failure as fatal).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Surl.Core's ServingEngine starts a listener per listen URL, dispatches connections by scheme with a fresh exchange context, isolates failures, and shuts down on a TimeProvider grace period
