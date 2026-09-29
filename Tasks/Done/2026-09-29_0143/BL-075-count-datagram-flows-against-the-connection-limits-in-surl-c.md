---
id: BL-075
title: Count datagram flows against the connection limits in Surl.Core
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-025, BL-032]
touches: [Surl.Core.UnitLibrary, Surl.Core.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-075 — Count datagram flows against the connection limits in Surl.Core

## Goal

Each datagram flow the serving engine dispatches counts as one connection against
`--max-connections` and `--max-connections-per-address`, gets the same idle timeout and
maximum exchange duration as a connection, and a flow past a limit is refused through
`IDatagramRefusalWriter` (TFTP error packet 0) or ended with no reply.

## Context

- ADR-0006 sections 1, 5 and 6: "A datagram flow (TFTP, ADR-0004 section 3) counts as one
  connection"; a flow past a limit goes to `IDatagramRefusalWriter.WriteRefusalAsync`,
  then is disposed; the maximum duration is measured from the flow's first datagram.
- BL-025 enforces the limits for connections only, because the engine refuses datagram
  schemes until BL-032 dispatches flows. Reuse BL-025's `ConnectionAdmission`,
  `ExchangeDeadlines` and `RefusalWriteDeadline` in `Surl.Core.UnitLibrary`; the idle
  clock restarts on every datagram received or sent, as `IdleClockRestartingConnection`
  does for bytes.

## Acceptance criteria

- [x] A fast test proves the flow past `--max-connections` (and one past
      `--max-connections-per-address`) is handed to `IDatagramRefusalWriter` with the
      matching `ConnectionRefusal`, then disposed, while the flows already running are
      unaffected.
- [x] A fast test proves a flow is cancelled at the idle timeout and at the maximum
      exchange duration and not before, on the hand-written `TimeProvider`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `Surl.Core.UnitLibrary`.

## Notes

- 2026-09-28, from BL-032: BL-032 dispatched flows through the same `ConnectionAdmission`,
  `ExchangeDeadlines` and `RefusalWriteDeadline` as connections, because a separate path
  would have been more code. Already proven in `Surl.Core.UnitTests/ServingEngineTests.Datagrams.cs`:
  refusal past `--max-connections` (`ServeAsync_FlowPastTheConnectionLimit_*`) and past
  `--max-connections-per-address` (`ServeAsync_FlowPastThePerAddressLimit_*`), cancellation
  at the idle timeout and at the maximum duration. The idle clock restarts on each datagram
  (`IdleClockRestartingDatagramFlow`, `IdleClockRestartingDatagramFlowTests`). What remains
  here is checking each criterion against those tests. The one clearly missing piece is
  "and not before" for the flow's idle and duration clocks; add it, then close this task.

- 2026-09-29, BL-075 run: the criteria were already met by BL-032's tests (listed above) apart from
  "and not before". Extended the two flow deadline tests to advance the hand-written
  `TimeProvider` one tick short of the idle timeout and of the maximum duration, assert the
  flow is neither cancelled nor disposed, then advance the last tick and assert both:
  `ServeAsync_FlowWithNoDatagramForTheIdleTimeout_IsCancelledAndDisposedThenAndNotBefore`,
  `ServeAsync_FlowThatReachesTheMaximumExchangeDuration_IsCancelledAndDisposedThenAndNotBefore`.
  No production change; Measure-CodeQuality reports Surl.Core.UnitLibrary 100/100, 0 failing.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Datagram flows count against --max-connections and --max-connections-per-address and are cancelled at the idle timeout and maximum duration, not before, proven by fast tests
