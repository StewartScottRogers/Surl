---
id: BL-1249
title: Carry the exchange and its listen URL through ContentCaller so content out-of-space rules match listener and scheme and content changes name their exchange (ADR-0168)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1239]
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests]
requirement: FR-062
created: 2026-11-16
completed: 2026-11-16
---
# BL-1249 — Carry the exchange and its listen URL through ContentCaller so content out-of-space rules match listener and scheme and content changes name their exchange (ADR-0168)

## Goal

ContentCaller carries the listen URL and has ForExchange(ExchangeContext, account); the content store fills FaultPoint.ListenUrl and every StoreChangedEvent and FaultAppliedEvent ExchangeId from it.

## Context

ADR-0168 decisions 1, 3 and 4 (Documentation/Planning/Decisions/ADR-0168-how-the-content-store-and-the-mail-store-learn-the-exchange-behind-a-write.md). Start at Foundation/Surl.Content.UnitLibrary/ContentCaller.cs and ContentStoreEventsAndFaults.cs RunsOutOfSpaceAsync; event sites in ContentStore.cs and ContentUploadSession.cs. No wire byte changes.

## Acceptance criteria

- [x] `ContentCaller.ForExchange(ExchangeContext, string?)` and `ListenUrl` exist; `ForAccount`/`Anonymous`/`WithExchange` behave as before.
- [x] A unit test proves `out-of-space;listener=...` and `out-of-space;scheme=...` fire for a caller built with ForExchange, and a seed (no exchange) still publishes ExchangeId null.
- [x] A unit test proves each content StoreChangedEvent and out-of-space FaultAppliedEvent carries the caller's ExchangeId.
- [x] 100% line and branch coverage held; build clean; fast tests green.

## Notes

Stored `ContentCaller.ListenUrl` as the reported string (ReportedListenUrl.Format), the form FaultPoint.ListenUrl matches. WithExchange keeps the listen URL.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. ContentCaller.ForExchange and ListenUrl; fault point carries listen URL
