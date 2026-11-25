---
id: BL-1677
title: Say in FR-015 that the HTTP server's cookies are built
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1662]
touches: [Documentation/Product/Requirements.md]
requirement: FR-015
created: 2026-11-24
completed:
---
# BL-1677 — Say in FR-015 that the HTTP server's cookies are built

## Goal

FR-015's status says what is built and proven instead of "unbuilt".

## Context

`Documentation/Product/Requirements.md`, row FR-015, reads "Draft (decided by BL-1650, ADR-0189; unbuilt)". BL-1659, BL-1660, BL-1661 and BL-1673 built it; BL-1662 is the pinned-curl proof.

## Acceptance criteria

- [ ] FR-015's status matches what BL-1662 proved, naming any part still unbuilt (HTTP/3 is BL-1674 and BL-1679).

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-25: Backlog -> Doing.
