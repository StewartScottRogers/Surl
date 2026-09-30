---
id: BL-176
title: Hand each exchange its data-connection opener and count data bytes against its idle clock in Surl.Core
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-174]
touches: [Surl.Core.UnitLibrary, Surl.Core.UnitTests]
requirement: FR-036
created: 2026-09-29
completed: 2026-09-29
---
# BL-176 — Hand each exchange its data-connection opener and count data bytes against its idle clock in Surl.Core

## Goal

`Surl.Core`'s `ServingEngine` gives each exchange the data-connection opener BL-173's ADR
describes, so every data connection's bytes restart the exchange's idle clock, appear in its
verbose and trace logs as the ADR says, count against the connection limits if the ADR says so,
and are disposed when the exchange ends.

## Context

- Decisions: BL-173's ADR (how Core receives the opener from composition, idle-clock and limit
  accounting, logging); ADR-0006 section 1 ("Bytes on an FTP data connection belong to the
  exchange that opened it, so a long FTP download never idles its control connection out");
  ADR-0033 (the exchange log and trace dump).
- Code: `Surl.Core.UnitLibrary/ServingEngine.cs` (builds `ExchangeContext` at the line that
  constructs it), `IdleClockRestartingConnection.cs`, `RecordingConnection.cs`,
  `ExchangeDeadlines.cs`, `ConnectionAdmission.cs`, `InFlightExchanges.cs`.
- The engine's constructor gains the opener as the ADR says, defaulting to BL-174's refusing
  default so `Surl.Console` compiles unchanged until BL-182 passes the real one.

## Acceptance criteria

- [x] Fast tests with BL-174's in-memory fake show: bytes on a data connection postpone the idle
      timeout of the control connection; a data connection still open when the exchange ends is
      disposed; data bytes are written to the verbose and trace logs as the ADR says; the
      connection limits count data connections as the ADR says.
- [x] `Surl.Console` builds unchanged (the default applies).
- [x] `dotnet build Surl.Core.UnitLibrary -warnaserror` is clean; the fast tests pass with no
      socket opened; `Measure-CodeQuality.ps1 -Library Surl.Core.UnitLibrary` reports 100% line
      and branch coverage and no failing member.

## Notes

- Built as ADR-0052 decision 9's `Surl.Core` bullet says; no new decision was needed, so no ADR.
- `ServingEngine` gains a seven-parameter constructor taking `IDataConnectionOpener`; the
  six-parameter one chains with `RefusingDataConnectionOpener.Instance`, so `Surl.Console` is
  unchanged until BL-182.
- Each connection exchange's context gets `DataConnections = new ExchangeDataConnections(...)`
  (`context with { ... }`). It wraps every data connection as
  `ExchangeDataConnection(IdleClockRestartingConnection(RecordingConnection(...)))`, notes
  `Data connection opened: passive from <remote>.` / `active to <remote>.` and, on the first
  dispose, `Data connection closed.`; `ExchangePassiveDataListener` routes accepts through it.
  After the server returns, the engine disposes whatever is still open (failures swallowed)
  before its `Exchange N ended` note.
- Limits: nothing to add - data connections never pass through `ConnectionAdmission`, get no
  exchange ID, and end with the exchange; `ServeAsync_OpenDataConnection_DoesNotCountAgainstMaxConnections`
  pins it.
- Datagram exchanges keep the refusing default: FTP is connection-only.
- Tests: `ExchangeDataConnectionsTests` (the wrapper, listener and connection together, since the
  two small wrappers only exist through it) and `ServingEngineTests.DataConnections.cs`.
  Core: 123 tests, 100% line and branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ServingEngine hands each exchange a data-connection opener whose bytes restart the idle clock and reach its log, disposed at exchange end
