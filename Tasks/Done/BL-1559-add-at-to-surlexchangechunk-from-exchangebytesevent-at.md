---
id: BL-1559
title: Add At to SurlExchangeChunk from ExchangeBytesEvent.At
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1552]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1559 — Add At to SurlExchangeChunk from ExchangeBytesEvent.At

## Goal

Each SurlExchangeChunk carries the At of the ExchangeBytesEvent it was kept from.

## Context

Follows BL-1552: the chunk list now keeps when each read or write happened.

## Acceptance criteria

- [x] SurlExchangeChunk.At is set from ExchangeBytesEvent.At by ExchangeRecordBook, covered by a test; Api and Mcp fast tests pass.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. At copied from ExchangeBytesEvent.At
