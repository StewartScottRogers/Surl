---
id: BL-1552
title: Decide whether a recorded exchange chunk carries the time its bytes crossed
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0099-one-server-composition-for-surl-and-surl-api-and-surl-apis-programming-interface.md, Documentation/Planning/Decisions/ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1552 — Decide whether a recorded exchange chunk carries the time its bytes crossed

## Goal

An ADR amendment, marked "Decided by Claude under Stewart's delegation", says whether `SurlExchangeChunk` and
`get_exchanges`' chunks carry the time their bytes were read or written, and if they do, under what name,
from which clock and in what form; any implementation it calls for is filed as tasks.

## Context

- Surly cookbook bug report 2026-11-21, item 3 (second half): "`get_exchanges` chunks carry no timestamps".
- Today: `SurlExchangeChunk(Direction, Bytes, Layer, StreamId)` (`Application/Surl.Api/SurlExchangeChunk.cs`)
  has no time; ADR-0099 decision 7 shows the record without one and ADR-0103 decision 3 shows the
  `get_exchanges` chunk as `{direction, encoding, data}` (plus `layer` and `stream_id`). Neither ADR decides
  against a time; it was never asked. Each chunk is made from an `ExchangeBytesEvent`
  (`Application/Surl.Api/ExchangeRecordBook.cs:38`, `:82`, `:116`), whose `SurlEvent.At` comes from the
  instance's injected `TimeProvider`, so the time exists where the chunk is made.
- Stand-in questions the decision must answer: a test using a `delay` fault (ADR-0124 decision 4) wants to see
  when the server answered; determinism needs the time from the injected `TimeProvider` (a test on a manual
  clock gets the same times every run); configuration only grows (an added positional record member must
  keep source compatibility, e.g. an optional parameter at the end); and the control endpoint's exchange
  writer (`Application/Surl.Control.UnitLibrary/ControlExchangeWriter.cs`) shows chunks too.

## Acceptance criteria

- [x] ADR-0099 (decision 7) or ADR-0103 (decision 3), whichever owns the shape, has an amendment saying yes or
  no, with the reasons by the standing rules.
- [x] If yes, the amendment names the member in `SurlExchangeChunk`, in `get_exchanges`' chunk JSON and in the
  control endpoint's exchange JSON, its source (`ExchangeBytesEvent.At`) and its format (ISO 8601 UTC with
  `"O"`, as `started_at` is written).
- [x] If yes, one task per project pair is filed with `task-board.ps1 new` (`Surl.Api`; `Surl.Mcp.UnitLibrary`;
  `Surl.Control.UnitLibrary`), each depending on this one, and the amendment names their IDs.
- [x] (No branch not taken: decision is yes, so no "no" amendment is needed.)
  `exchange_started` and `exchange_ended` times), and the Log line of this task says no task was filed.

## Notes

Decided yes: `at` from `ExchangeBytesEvent.At`, optional last parameter, ISO 8601 "O". Filed BL-1559, BL-1557, BL-1558.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. Decided yes: chunks carry at (ADR-0099 amendment 2026-11-21, ADR-0103 amendment 2); filed BL-1559, BL-1557, BL-1558
