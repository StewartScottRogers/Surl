---
id: BL-339
title: Fold BL-316's upload choices into ADR-0074 decision 6
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-316]
touches: [Documentation/Planning/Decisions/ADR-0074-how-the-rtsp-server-answers-upstream-curl.md]
requirement: FR-051
created: 2026-09-30
completed:
---
# BL-339 — Fold BL-316's upload choices into ADR-0074 decision 6

## Goal

ADR-0074 decision 6 states the upload behaviours BL-316 chose where the decision was silent, so the ADR again says everything Surl.Protocol.Rtsp does with ANNOUNCE, RECORD and received interleaved frames.

## Context

BL-316 built decision 6 in Surl.Protocol.Rtsp.UnitLibrary while Documentation/Planning/Decisions was held by BL-287, so its choices went into BL-316's Notes and Surl.Protocol.Rtsp.UnitLibrary/CLAUDE.md instead. Read BL-316's Notes (Tasks/Done or its archive) for the list and the reasons. Same shape as BL-338 for BL-315.

## Acceptance criteria

- [ ] ADR-0074 decision 6 states each choice in BL-316's Notes - a SETUP cannot change a session's mode (455), a refused path is 403 for ANNOUNCE and SETUP to record, a TEARDOWN whose commit fails is 403 with the session ended, an ANNOUNCE the store's own limit refuses is 403, a recording session does not time out while recording, frames are told from heads only after the first head - each marked `Decided by Claude under Stewart's delegation` in BL-316.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
