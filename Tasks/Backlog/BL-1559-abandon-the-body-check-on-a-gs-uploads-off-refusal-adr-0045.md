---
id: BL-1559
title: Abandon the body check on a gs uploads-off refusal (ADR-0045 amendment 1)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1553, BL-1556, BL-1557]
touches: [Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Surl.Extended.Protocol.GoogleCloudStorage.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1559 — Abandon the body check on a gs uploads-off refusal (ADR-0045 amendment 1)

## Goal

gs calls AbandonAsync when an uploads-off refusal skips the body, so an HMAC PUT shows login_judged as a Bearer PUT does.

## Context

ADR-0045 amendment 1 (BL-1553) decides what an abandoned body-bound login publishes. Peer-visible answers must not change.

## Acceptance criteria

- [ ] GcsPutObjectTests: HMAC PUT with a sent hash, uploads off, publishes login_judged is_accepted true; the 403 is unchanged.
- [ ] Build clean, fast tests green, 100% coverage held.

## Notes

## Log

- 2026-11-21: Created.
