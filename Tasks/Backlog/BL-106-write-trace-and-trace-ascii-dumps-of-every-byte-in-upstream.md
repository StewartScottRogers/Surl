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
completed:
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

- [ ] Tests in `Surl.Output.UnitTests` feed the request bytes of the existing HTTP fixture
      `Surl.Protocol.Http.UnitTests/Fixtures/get-file/request.bin` (copied into the test as
      a byte literal, or read by relative path the way other tests do) as received bytes and
      a short response as sent bytes, and pin the `--trace` output and the `--trace-ascii`
      output exactly as ADR-0033 decision 4 lays them out.
- [ ] Tests cover an empty call (writes nothing), a chunk longer than one dump row, a byte
      outside 0x20 to 0x7E in the ASCII column, notes, and `--trace-time` stamps from a fake
      `TimeProvider`.
- [ ] Two concurrent exchanges' dumps never interleave inside one event block.
- [ ] `dotnet build Surl.Output.UnitLibrary -warnaserror` is clean; the fast tests pass;
      100% line and branch coverage kept; no method exceeds complexity 10.

## Notes

## Log

- 2026-09-29: Created.
