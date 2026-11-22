---
id: BL-1557
title: Write at on each get_exchanges chunk
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1552, BL-1559]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1557 — Write at on each get_exchanges chunk

## Goal

Each chunk in `get_exchanges` carries `at`, the time its bytes crossed.

## Context

ADR-0103 amendment 2 (BL-1552); `SurlExchangeChunk.At` exists since BL-1559. File: `RecordedExchangeWriter.cs`.

## Acceptance criteria

- [x] Every chunk object has `at`, ISO 8601 UTC with "O" as `started_at` is written; test `WriteExchanges_ChunkWithATime_WritesAtAsIso8601Utc`.

## Notes

`at` is written after `layer`/`stream_id`, before `encoding`; format matches `started_at` (`+00:00` suffix from "O" on a UTC DateTimeOffset).

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. get_exchanges chunks carry at (ISO 8601 UTC)
