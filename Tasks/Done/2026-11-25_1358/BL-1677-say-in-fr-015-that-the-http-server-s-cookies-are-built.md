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
completed: 2026-11-25
---
# BL-1677 — Say in FR-015 that the HTTP server's cookies are built

## Goal

FR-015's status says what is built and proven instead of "unbuilt".

## Context

`Documentation/Product/Requirements.md`, row FR-015, reads "Draft (decided by BL-1650, ADR-0189; unbuilt)". BL-1659, BL-1660, BL-1661 and BL-1673 built it; BL-1662 is the pinned-curl proof.

## Acceptance criteria

- [x] FR-015's status matches what BL-1662 proved, naming any part still unbuilt (HTTP/3 is BL-1674 and BL-1679).

## Notes

- BL-1662 had already replaced "unbuilt" with "Proven by", so this task corrected what was left
  stale: BL-1674 is done, and only BL-1679 is open (`ComposeHttp3ProtocolServer` passes no
  cookies, checked in `SurlServerComposition.Start.cs`).
- The row now states the proof's limits (`-c`, `-b`, `-b -j` over `http`, HTTP/1.1, Windows), the
  `Surl.Api` and `Surl.Mcp` surfaces (BL-1675, BL-1676), and updates the document's "Last updated".
- Done in the session without the `align-and-document` subagent: one table row and a date. No
  `.cs` or project file changed. `dotnet build` 0 warnings, 0 errors; fast tests green.

## Log

- 2026-11-24: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. FR-015 states what is built and proven by pinned curl, and that HTTP/3 cookies are unbuilt until BL-1679
