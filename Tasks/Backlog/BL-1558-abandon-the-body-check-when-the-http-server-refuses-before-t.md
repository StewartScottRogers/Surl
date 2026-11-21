---
id: BL-1558
title: Abandon the body check when the HTTP server refuses before the body (ADR-0045 amendment 1)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1553, BL-1556, BL-1557]
touches: [Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1558 — Abandon the body check when the HTTP server refuses before the body (ADR-0045 amendment 1)

## Goal

The HTTP server calls AbandonAsync on the verdict body check whenever it answers without reading the body, with the answer unchanged.

## Context

ADR-0045 amendment 1 (BL-1553) decides what an abandoned body-bound login publishes. Peer-visible answers must not change.

## Acceptance criteria

- [ ] HttpProtocolServer tests: an uploads-off signed PUT publishes the event and the status, body and delay are as before.
- [ ] Build clean, fast tests green, 100% coverage held.

## Notes

## Log

- 2026-11-21: Created.
