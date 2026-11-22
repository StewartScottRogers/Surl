---
id: BL-1564
title: Abandon the body check on a gs uploads-off refusal (ADR-0045 amendment 1)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1553, BL-1561, BL-1562]
touches: [Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Surl.Extended.Protocol.GoogleCloudStorage.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1564 — Abandon the body check on a gs uploads-off refusal (ADR-0045 amendment 1)

## Goal

gs calls AbandonAsync when an uploads-off refusal skips the body, so an HMAC PUT shows login_judged as a Bearer PUT does.

## Context

ADR-0045 amendment 1 (BL-1553) decides what an abandoned body-bound login publishes. Peer-visible answers must not change.

## Acceptance criteria

- [x] GcsPutObjectTests: HMAC PUT with a sent hash, uploads off, publishes login_judged is_accepted true; the 403 is unchanged.
- [x] Build clean, fast tests green, 100% coverage held.

## Notes

GcsPutObject calls bodyCheck.AbandonAsync on the uploads-off refusal. The gs test policy records the abandonment (AbandonedBodyChecks); the published login_judged event itself is the session behaviour BL-1562 already tests. Other early refusals are left as the task scopes only uploads-off.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. gs PUT abandons the body check on an uploads-off refusal
