---
id: BL-106
title: Write --trace and --trace-ascii dumps of every byte in upstream curl's layout
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-105]
touches: [Surl.Output.UnitLibrary, Surl.Output.UnitTests]
requirement: FR-013
created: 2026-09-29
completed: 2026-09-29
---
# BL-106 — Write --trace and --trace-ascii dumps of every byte in upstream curl's layout

## Goal

`Surl.Output` writes the trace level's `--trace` (hex and ASCII) and `--trace-ascii` dumps of
every byte each exchange receives and sends, in the layout ADR-0033 pinned from upstream
curl 8.21.0, to an injected `TextWriter`.

## Context

FR-013; ADR-0033 (BL-101) decision 4 gives the layout measured from the pinned build's
`--trace` and `--trace-ascii` (event header lines, offsets, hex and ASCII columns, labels
turned to the server's side, the exchange id), and decision 5 which lines `--trace-time`
stamps. BL-105 made the log factory level-aware; this task adds the trace level's writers
beside it. Composition (opening the file, `-` for stdout) is BL-107.

- `Surl.Output.UnitLibrary/VerboseExchangeLog.cs` shows the existing shape: an
  `IExchangeLog` (`BytesReceived`, `BytesSent`, `Note`) writing whole lines under a shared
  `Lock`.
- The engine calls `IExchangeLog` from `Surl.Core`'s recording decorators with plaintext
  bytes (after TLS), so the dump is plaintext, as curl's `--trace` is.
- Write through injected writers only (`Surl.Output.UnitLibrary/CLAUDE.md`).

## Acceptance criteria

- [x] Tests in `Surl.Output.UnitTests` feed the request bytes of the existing HTTP fixture
      `Surl.Protocol.Http.UnitTests/Fixtures/get-file/request.bin` (copied into the test as
      a byte literal, or read by relative path the way other tests do) as received bytes and
      a short response as sent bytes, and pin the `--trace` output and the `--trace-ascii`
      output exactly as ADR-0033 decision 4 lays them out.
- [x] Tests cover an empty call (writes nothing), a chunk longer than one dump row, a byte
      outside 0x20 to 0x7E in the ASCII column, notes, and `--trace-time` stamps from a fake
      `TimeProvider`.
- [x] Two concurrent exchanges' dumps never interleave inside one event block.
- [x] `dotnet build Surl.Output.UnitLibrary -warnaserror` is clean; the fast tests pass;
      100% line and branch coverage kept; no method exceeds complexity 10.

## Notes

- Shape: `TraceExchangeLogFactory` (public, beside `LevelledExchangeLogFactory`) takes the
  writer, a `TraceDumpLayout` (`HexAndAscii` for `--trace`, `Ascii` for `--trace-ascii`),
  the `--trace-time` switch and a `TimeProvider`. Its `TraceExchangeLog` writes each
  non-empty `BytesReceived`/`BytesSent` call as one event under the shared lock; notes and
  `NoteOutsideExchange` reuse `VerboseExchangeLog`'s note line, so the escaping and the
  `#-` form stay one implementation. `TraceDumpRows` lays out the rows.
- Chosen: kept the trace factory separate from `LevelledExchangeLogFactory` rather than
  teaching the latter `LogLevel.Trace`, since the dump needs a layout the other levels do
  not; BL-107 picks the factory from the parsed level. ADR-0033 section 4 already decides
  every byte, so no new ADR.
- The `--trace-ascii` row break ports curl's `dump()` rule exactly: a CR LF pair that starts
  anywhere in the row, or immediately after a full 64-byte row, ends the row and is skipped.
  Tests pin the 63/64/65-byte edges, a lone CR and LF, and an empty CR LF row.
- The fixture request is copied as a byte literal (87 bytes); a second test pins ADR-0033's
  measured 79-byte request event row for row in both layouts.
- `FixedTimeProvider` and `CharByCharWriter` moved out of `LevelledExchangeLogFactoryTests`
  into their own test files so both test classes share them.
- `Measure-CodeQuality.ps1 -Library Surl.Output.UnitLibrary`: 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Surl.Output writes --trace and --trace-ascii dumps of every byte in upstream curl 8.21.0's layout, with --trace-time stamps
