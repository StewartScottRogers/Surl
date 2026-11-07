---
id: BL-784
title: Point S3:Surl-Limits-1 at the test that pins 400 RequestHeaderSectionTooLarge
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-720]
touches: [Documentation/Conformance/Extended/S3.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-784 — Point S3:Surl-Limits-1 at the test that pins 400 RequestHeaderSectionTooLarge

## Goal

The `S3:Surl-Limits-1` row of `Documentation/Conformance/Extended/S3.md` names the test that now pins it instead of `BL-418`.

## Context

- BL-720 made a head past `--max-request-head` on the `s3` server answer `400 RequestHeaderSectionTooLarge`, pinned by
  `S3ProtocolServerTests.ServeAsync_HeadPastMaxRequestHead_IsRefused400RequestHeaderSectionTooLargeAndClosed`.
- BL-720 could not edit the matrix: `Documentation/Conformance` was in the `touches` of BL-395, then in Doing.

## Acceptance criteria

- [x] The `S3:Surl-Limits-1` row's last column is `S3ProtocolServerTests.ServeAsync_HeadPastMaxRequestHead_IsRefused400RequestHeaderSectionTooLargeAndClosed`.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. S3.md's S3:Surl-Limits-1 row names the test that pins 400 RequestHeaderSectionTooLarge
