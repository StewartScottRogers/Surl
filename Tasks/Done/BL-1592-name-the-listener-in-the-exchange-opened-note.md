---
id: BL-1592
title: Name the listener in the Exchange opened note
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1591]
touches: [Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-1592 — Name the listener in the Exchange opened note

## Goal

`ServingEngine` writes the `Exchange <id> opened` note in the form BL-1591's ADR-0033 amendment decides, naming the
listen URL that accepted the exchange, so two listeners of one scheme are told apart in `-v`, `--trace-ascii` and
`--log-file`.

## Context

- Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T0726-Surl-bug-report.md` item 4.
- The note is written at `Foundation/Surl.Core.UnitLibrary/ServingEngine.cs:1003`
  (`log.Note($"Exchange {exchangeId} opened: {listenUrl.Scheme} from {remoteEndPoint}.")`); the
  `ExchangeStartedEvent` on line 1004 already formats the listener with `ReportedListenUrl.Format(listenUrl)`.
- Tests that pin the old text and change with it: `Foundation/Surl.Core.UnitTests/ServingEngineTests.DataConnections.cs:112`
  and any other `Exchange 1 opened:` assertion in `Surl.Core.UnitTests`, and the `OpenedLine` constant at
  `Application/Surl.Console.UnitTests/CommandLineRunnerLogTests.cs:20` (a real engine run). `Surl.Output.UnitTests` feeds
  notes in by hand and needs no change; the prefix `Exchange <id> opened: ` is kept (ADR-0033 amendment), so
  `LevelledExchangeLogFactory` still writes it at `info`.
- Stand-in: no store or event change; `ExchangeStartedEvent` is unchanged.

## Acceptance criteria

- [x] `ServingEngine.cs` writes the note exactly as the ADR-0033 amendment from BL-1591 states, for connections and
      datagram flows.
- [x] A new fast test in `Surl.Core.UnitTests` serves two listen URLs of one scheme on different ports and asserts each
      exchange's note names its own listen URL.
- [x] A fast test pins the datagram-flow form (for example a `tftp` flow).
- [x] `CommandLineRunnerLogTests.OpenedLine` and every other pinned old note are updated; `grep -rn "opened: http from"` over
      `Foundation/Surl.Core.UnitTests` and `Application/Surl.Console.UnitTests` finds only the new form.
- [x] `dotnet build Foundation/Surl.Core.UnitLibrary -warnaserror` clean, fast tests green, `Surl.Core.UnitLibrary` keeps
      100% line and branch coverage.

## Notes

- Wrote the ADR-0033 amendment 1 form via ReportedListenUrl.Format (no extra escaping: ListenUrl hosts are validated names; no Unix-socket listener exists in Format). Added ServingEngineTests.OpenedNote.cs (two http listeners; tftp flow). Core 321 tests, Console 549 pass.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
- 2026-11-22: Doing -> Done. Exchange opened note names its listen URL; tests pin connection and datagram forms
