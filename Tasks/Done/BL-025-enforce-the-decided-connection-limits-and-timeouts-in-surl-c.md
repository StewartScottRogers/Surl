---
id: BL-025
title: Enforce the decided connection limits and timeouts in Surl.Core
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-015, BL-024]
touches: [Surl.Core.UnitLibrary, Surl.Core.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-025 — Enforce the decided connection limits and timeouts in Surl.Core

## Goal

The serving engine from BL-015 enforces every connection-level limit that the hardening
ADR (BL-024) assigns to `Surl.Core`, with the ADR's default numbers and its behaviour
when a limit is hit. Fast tests prove each limit on a hand-written `TimeProvider`.

## Context

- The hardening ADR recorded by BL-024 under `Documentation/Planning/Decisions/` is the
  specification. Its `README.md` index names it. This task covers only the rows the ADR
  assigns to `Surl.Core`: expect concurrent connections (total and per remote address),
  idle timeout and maximum exchange duration. Limits inside a protocol (head size,
  command-line length) belong to the protocol servers' own tasks.
- The serving engine and its fakes are in `Surl.Core.UnitLibrary` and
  `Surl.Core.UnitTests` (BL-015). Time comes only from the injected `TimeProvider`. No
  `Thread.Sleep`, and no package (no `FakeTimeProvider`).
- The limit values reach `Surl.Core` through the parsed command line. If BL-014 has not
  added the options yet, take them as constructor parameters with the ADR's defaults,
  and file a `Surl.Cli` task for the options.

## Acceptance criteria

- [x] One fast test per `Surl.Core` limit in the ADR proves the default number and the
      ADR's behaviour at the limit: the connection past the limit is closed or refused
      as the ADR says, and existing exchanges are unaffected.
- [x] Idle-timeout and maximum-duration tests advance the hand-written `TimeProvider`
      and assert the exchange is cancelled at the ADR's time and not before.
- [x] `dotnet build Surl.Core.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Core.UnitLibrary`.

## Notes

Plan, as built (ADR-0006 sections 1, 5 and 6):

- `ConnectionLimits` (public record, `Surl.Core`): `MaxConnections` 1024,
  `MaxConnectionsPerAddress` 100, `IdleTimeout` 120 s, `MaxExchangeDuration` 3600 s in
  `Default`; 0 is no limit; `None` is all zeros. A new `ServingEngine` constructor takes
  one; the old constructor uses `ConnectionLimits.Default`, so `Surl.Console` enforces the
  ADR's defaults today without a change outside this task's `touches`. The options are
  already parsed by `Surl.Cli` (`SurlCommandLine`); passing them through is BL-068.
- `ConnectionAdmission` counts connections in total and per remote IP address
  (IPv4-mapped IPv6 folded to IPv4; an endpoint with no IP counts only against the
  total). It is checked on the accept loop; a connection past a limit is never counted
  and never given an `ExchangeId`.
- A refused connection on a plaintext listener goes to the server's
  `IConnectionRefusalWriter`, if it has one, with `ServingEngine.RefusalWriteDeadline`
  (1 s, on the engine's `TimeProvider`); the engine stops waiting at the deadline even if
  the writer ignores its token. An implicit-TLS listener (`TlsSchemes.IsImplicitTls`)
  gets a bare close with no handshake. The connection is then disposed, not aborted,
  unless the writer threw something other than a cancellation.
- `ExchangeDeadlines` gives each exchange one token linked to shutdown, an idle clock and
  a duration clock (`CancellationTokenSource(timeout, TimeProvider)`, so no timer of our
  own). `IdleClockRestartingConnection` restarts the idle clock when a read returns bytes
  or a write completes, so a stalled write counts as idle. An exchange ended by a limit
  closes gracefully (disposed, not aborted) and its log notes which limit and the number,
  e.g. `Exchange 1 cancelled: no byte moved for the idle timeout of 120 s.`

Choices taken as sensible defaults (rule 1):

- A connection's slot is released after the connection is disposed (in a `finally`), so
  a dispose that lingers still counts against the limit.
- `ConnectionLimits` refuses a duration above 4294967294 ms (`MaxTimeout`), the most a
  `CancellationTokenSource` counts down; BL-068 maps that to exit code 2 on the command
  line.
- The cancellation note gives the first reason that fired (recorded by a registration on
  the exchange token), falling back to the reason that holds now, shutdown first.
- Refusals are not logged yet: `IExchangeLogFactory.Create` needs an `ExchangeId`, and a
  refused connection has none (ADR-0006 section 5). Changing that contract is outside
  this task's `touches`; filed as BL-070.
- Datagram flows are not counted yet: the engine refuses datagram schemes until BL-032
  dispatches flows. Filed as BL-071.

Review (code-reviewer) found, and this task fixed: the admission release moved into a
`finally`; a hard stop for a refusal writer that ignores its token; the first-fired
reason in the cancellation note; `RestartIdleClock` after dispose is a no-op; an
`ExchangeDeadlinesTests` class. Also filed: BL-069 (`StreamConnection` can leak its socket
when a graceful close after a cancelled write throws something other than `IOException`,
and can block without bound on TLS).

Test harness lesson: the fake server records an exchange before running its script, so a
test harness that releases held exchanges at shutdown must also release any registered
after that (a 1-in-17 hang before the fix).

Results: 74 `Surl.Core.UnitTests` (30 repeated runs green), whole-solution fast tests
green, `Measure-CodeQuality.ps1 -Library Surl.Core.UnitLibrary`: 100% line, 100% branch,
0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. The serving engine refuses connections past 1024 total or 100 per address and cancels exchanges idle 120 s or older than 3600 s
