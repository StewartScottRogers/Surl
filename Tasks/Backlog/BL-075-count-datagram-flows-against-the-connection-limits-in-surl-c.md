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
completed:
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

- [ ] A fast test proves the flow past `--max-connections` (and one past
      `--max-connections-per-address`) is handed to `IDatagramRefusalWriter` with the
      matching `ConnectionRefusal`, then disposed, while the flows already running are
      unaffected.
- [ ] A fast test proves a flow is cancelled at the idle timeout and at the maximum
      exchange duration and not before, on the hand-written `TimeProvider`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `Surl.Core.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
