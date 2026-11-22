---
id: BL-1560
title: Abandon the body check on an s3 refused write (ADR-0045 amendment 1)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1553, BL-1561, BL-1562]
touches: [Surl.Extended.Protocol.S3.UnitLibrary, Surl.Extended.Protocol.S3.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1560 — Abandon the body check on an s3 refused write (ADR-0045 amendment 1)

## Goal

s3 calls AbandonAsync when an uploads-off refusal (PutObject and multipart writes) skips the body.

## Context

ADR-0045 amendment 1 (BL-1553) decides what an abandoned body-bound login publishes. Peer-visible answers must not change.

## Acceptance criteria

- [x] S3PutObjectTests: a signed PUT with a sent hash, uploads off, publishes login_judged is_accepted true; the 403 is unchanged.
- [x] Build clean, fast tests green, 100% coverage held.

## Notes

- The abandon call sits on every pre-body refusal of PutObject (uploads off), UploadPart and CompleteMultipartUpload, via `S3LoginResult.AbandonBodyCheckAsync` (no-op when the head decided). The login_judged event itself is published by Surl.Authentication (BL-1562); here the hand-written test policy counts the calls. Answers are unchanged.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. s3 abandons the body check on an uploads-off refused write
