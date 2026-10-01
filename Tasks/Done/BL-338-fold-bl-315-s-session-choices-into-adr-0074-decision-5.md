---
id: BL-338
title: Fold BL-315's session choices into ADR-0074 decision 5
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-315]
touches: [Documentation/Planning/Decisions/ADR-0074-how-the-rtsp-server-answers-upstream-curl.md]
requirement: FR-051
created: 2026-09-30
completed: 2026-09-30
---
# BL-338 — Fold BL-315's session choices into ADR-0074 decision 5

## Goal

ADR-0074 decision 5 states the session behaviours BL-315 chose where the decision was silent, so the ADR again says everything Surl.Protocol.Rtsp does with sessions.

## Context

BL-315 built decision 5 in Surl.Protocol.Rtsp.UnitLibrary. Documentation/Planning/Decisions was held by BL-322 at the time, so its choices went into BL-315's Notes and Surl.Protocol.Rtsp.UnitLibrary/CLAUDE.md instead. Read BL-315's Notes (Tasks/Done or its archive) for the list and the reasons.

## Acceptance criteria

- [x] ADR-0074 decision 5 states each choice in BL-315's Notes - the half-closed client streamed to the end, 455 for a SETUP naming the session for another presentation, PLAY while playing carrying on, Session named back on refusals, the timeout checked when a request names the session, the sender report's RTP timestamp and counts - each marked `Decided by Claude under Stewart's delegation` in BL-315.

## Notes

Done directly (a short ADR edit, no code): decision 5 gains a "Where this decision was first silent" list with BL-315's seven behaviour choices, marked decided by Claude under Stewart's delegation in BL-315. The two BL-316 hand-offs in BL-315's Notes (`mode=record` is `501` until BL-316; client `$` frames) are left out: they describe interim state that BL-316 replaces, not decision 5's behaviour. "Never while playing or recording" for the timeout comes from Surl.Protocol.Rtsp.UnitLibrary/CLAUDE.md.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ADR-0074 decision 5 states BL-315's session choices
