---
id: BL-1550
title: State the per-exchange cut and its is_cut, shown_bytes and recorded_bytes members in get_exchanges' description
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1550 — State the per-exchange cut and its is_cut, shown_bytes and recorded_bytes members in get_exchanges' description

## Goal

An agent reading `get_exchanges`' tool description learns that at most 65536 bytes of each exchange (and 1 MiB
per result) are shown, and that `is_cut`, `shown_bytes` and `recorded_bytes` say when and how much was cut,
instead of reading "byte for byte" and taking a cut exchange for the whole one.

## Context

- Surly cookbook bug report 2026-11-21, item 2: `get_exchanges` showed 65,536 bytes of a 200 KiB exchange
  "with no marker that it was cut". The marker exists and is decided: ADR-0103 decision 3 ("The cap") and
  `Application/Surl.Mcp.UnitLibrary/RecordedExchangeWriter.cs:19` (`MaxShownBytesPerExchange = 65536`),
  `:22` (`MaxShownBytesPerResult`), `:86-89` (writes `recorded_bytes`, `shown_bytes`, `is_cut`,
  `is_truncated`), present since BL-626 (commit `a48c5f2d5`, 2026-11-05), so also in the reported `9ad60d80c`.
  The cap stays; nothing about it changes.
- What misled the reporter: `GetExchangesTool.Description`
  (`Application/Surl.Mcp.UnitLibrary/GetExchangesTool.cs:21-23`) says "byte for byte" and names no cap or
  cut member. That is a "say what it does" defect in the tool's own words.
- Tests: `Application/Surl.Mcp.UnitTests/GetExchangesToolTests.cs`.
- Stand-in: no store, event or fault changes.

## Acceptance criteria

- [ ] `GetExchangesTool.Description` states that at most 65536 bytes of each exchange and 1 MiB in all are
  shown, and that a cut exchange has `is_cut` true, `shown_bytes` the bytes shown and `recorded_bytes` the
  bytes recorded; the numbers are built from `RecordedExchangeWriter.MaxShownBytesPerExchange` and
  `MaxShownBytesPerResult`, not typed again.
- [ ] A test in `GetExchangesToolTests` asserts the description names `is_cut`, `shown_bytes`,
  `recorded_bytes` and the two limits.
- [ ] `dotnet build Application/Surl.Mcp.UnitLibrary -warnaserror` is clean and
  `dotnet test Application/Surl.Mcp.UnitTests --filter "TestCategory!=Integration"` passes.
- [ ] `Application/Surl.Mcp.UnitLibrary/CLAUDE.md`'s `GetExchangesTool` entry says the description names the cut.

## Notes

## Log

- 2026-11-21: Created.
