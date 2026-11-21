---
id: BL-1558
title: Write at on each control endpoint exchange chunk
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1552, BL-1559]
touches: [Application/Surl.Control.UnitLibrary, Application/Surl.Control.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1558 — Write at on each control endpoint exchange chunk

## Goal

`GET /v1/exchanges` on the control endpoint writes `at` on each chunk (ADR-0099 amendment 2026-11-21, ADR-0103 amendment 2).

## Context

Follows BL-1552 (decision) and BL-1559 (`SurlExchangeChunk.At`). Files: `ControlExchangeWriter.cs`, `ControlJson.cs`.

## Acceptance criteria

- [x] Each chunk in the exchange JSON ends with `"at"`, the `ExchangeBytesEvent.At` as ISO 8601 UTC (`"O"`).
- [x] `FindExchangesAsync_HeldExchanges_AnswersTheGetExchangesShapeWithListenUrlAndScheme` pins it; Control tests 248/248 pass; build clean.

## Notes

Task file was an empty template; filled from BL-1552. `at` is always written (log events always carry a time), placed after `stream_id`.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. control endpoint exchange chunks carry at
