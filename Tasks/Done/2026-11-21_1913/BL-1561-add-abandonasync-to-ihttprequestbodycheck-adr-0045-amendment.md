---
id: BL-1561
title: Add AbandonAsync to IHttpRequestBodyCheck (ADR-0045 amendment 1)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1553]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1561 — Add AbandonAsync to IHttpRequestBodyCheck (ADR-0045 amendment 1)

## Goal

IHttpRequestBodyCheck has AbandonAsync(CancellationToken), a default no-op, that a server calls when it answers without reading the body.

## Context

ADR-0045 amendment 1 (BL-1553) decides what an abandoned body-bound login publishes. Peer-visible answers must not change.

## Acceptance criteria

- [x] HttpRequestBodyCheckTests.AbandonAsync_ByDefault_CompletesWithoutEffect passes.
- [x] Build clean, fast tests green, 100% coverage held.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. IHttpRequestBodyCheck.AbandonAsync default no-op added
