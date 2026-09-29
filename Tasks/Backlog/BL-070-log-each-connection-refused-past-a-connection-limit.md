---
id: BL-070
title: Log each connection refused past a connection limit
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-025]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests, Surl.Core.UnitLibrary, Surl.Core.UnitTests, Surl.Output.UnitLibrary, Surl.Output.UnitTests, Surl.Console, Surl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-070 — Log each connection refused past a connection limit

## Goal

With `-v`, each connection the serving engine refuses past `--max-connections` or
`--max-connections-per-address` leaves one verbose-log line naming the remote endpoint
and the limit it passed.

## Context

- ADR-0006, "Decision": no limit being reached ends the process; "the verbose log notes
  which limit and why". BL-025 notes idle-timeout and maximum-duration cancellations in
  the exchange's log, but a refused connection has no `ExchangeId` (ADR-0006 section 5)
  and `IExchangeLogFactory.Create` needs one, so BL-025 logs nothing for a refusal.
- Decide how an event outside any exchange reaches the verbose log (a new member on
  `IExchangeLogFactory`, or an engine-level log in `Surl.Protocol.Abstractions`), and the
  line's format by ADR-0007's verbose-log format. Record the decision in an ADR.
- The refusal is in `Surl.Core.UnitLibrary/ServingEngine.cs`, `RefuseAsync`.

## Acceptance criteria

- [ ] A `Surl.Core.UnitTests` test proves a refusal for each `ConnectionRefusal` value
      logs one line naming the remote endpoint and the limit.
- [ ] The decision and the line format are recorded in an ADR, and ADR-0007's verbose-log
      section points to it.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in the libraries touched.

## Notes

## Log

- 2026-09-28: Created.
