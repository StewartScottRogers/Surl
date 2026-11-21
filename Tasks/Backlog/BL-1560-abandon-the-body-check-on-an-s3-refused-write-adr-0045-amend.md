---
id: BL-1560
title: Abandon the body check on an s3 refused write (ADR-0045 amendment 1)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1553, BL-1556, BL-1557]
touches: [Surl.Extended.Protocol.S3.UnitLibrary, Surl.Extended.Protocol.S3.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1560 — Abandon the body check on an s3 refused write (ADR-0045 amendment 1)

## Goal

s3 calls AbandonAsync when an uploads-off refusal (PutObject and multipart writes) skips the body.

## Context

ADR-0045 amendment 1 (BL-1553) decides what an abandoned body-bound login publishes. Peer-visible answers must not change.

## Acceptance criteria

- [ ] S3PutObjectTests: a signed PUT with a sent hash, uploads off, publishes login_judged is_accepted true; the 403 is unchanged.
- [ ] Build clean, fast tests green, 100% coverage held.

## Notes

## Log

- 2026-11-21: Created.
