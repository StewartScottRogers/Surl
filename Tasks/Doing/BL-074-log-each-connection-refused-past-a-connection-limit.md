---
id: BL-074
title: Log each connection refused past a connection limit
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-025]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests, Surl.Core.UnitLibrary, Surl.Core.UnitTests, Surl.Output.UnitLibrary, Surl.Output.UnitTests, Surl.Console, Surl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-074 — Log each connection refused past a connection limit

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

- 2026-09-29 (lane 3): `Documentation/Planning/Decisions` added to `touches`: the
  acceptance criteria need a new ADR and an edit to ADR-0007, and `touches` did not name
  the folder. BL-061, in Doing on another lane, touches it, so this task went back to
  Backlog (dark factory rule 3). The code was written, built clean and tested green, and
  left uncommitted for the shift to stash (Measure-CodeQuality.ps1: 0 failing members); if the stash is gone, redo it from this design:
  - Seam (decided): a new member `void NoteOutsideExchange(string text)` on
    `IExchangeLogFactory`, not a separate engine log, because the factory already owns the
    `-v` writer and its lock, so the line cannot interleave with exchange lines and no new
    injection is needed. Implementers: `VerboseExchangeLogFactory` and the Core tests'
    `FakeExchangeLogFactory` (records `NotesOutsideExchanges`, can throw via
    `NoteOutsideExchangeFailure`).
  - Line format (decided): `#- * <text>`, escaped like any note: `-` stands where the
    exchange id goes, since exchange ids start at 1 and a refusal has none; the marker and
    columns stay those of ADR-0007 section 8. `VerboseExchangeLog.OutsideAnyExchange`
    builds that log.
  - Text (decided): `Refused a connection from <remote>: past --max-connections <n>.` or
    `... past --max-connections-per-address <n>.`; a datagram flow says `a flow`. Written
    by `ServingEngine.NoteRefusal`, called first in `RefuseAsync` and `RefuseFlowAsync`;
    an exception from the log is swallowed so the refusal still happens.
  - Tests: `ServeAsync_ConnectionPastALimit_LogsOneNoteOutsideAnyExchange_NamingTheRemoteEndPointAndTheLimit`
    (a DataRow per `ConnectionRefusal`), `..._WhoseNoteTheLogFailsToTake_IsStillRefusedAndClosed`,
    the flow refusal test asserting its note, and four `NoteOutsideExchange_*` tests in
    `VerboseExchangeLogFactoryTests`.
  - Still to do once BL-061 is Done: write ADR-0024 (next free number then) with the
    above, add it to the Decisions README, and point ADR-0007 section 8's engine-notes
    bullet to it.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Needs Documentation/Planning/Decisions for its ADR, which BL-061 (in Doing) touches; code is done, see Notes
- 2026-09-29: Backlog -> Doing.
