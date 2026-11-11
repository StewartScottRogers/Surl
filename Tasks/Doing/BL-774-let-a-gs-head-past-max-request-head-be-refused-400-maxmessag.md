---
id: BL-774
title: Let a gs head past --max-request-head be refused 400 MaxMessageLengthExceeded
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-431, BL-720]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-774 — Let a gs head past --max-request-head be refused 400 MaxMessageLengthExceeded

## Goal

A request head past `--max-request-head` on the `gs` server is answered `400 Bad Request` with the XML API
`Error` body `<Code>MaxMessageLengthExceeded</Code>`, then closed, as ADR-0110 decision 7 says.

## Context

- Today `Surl.HttpServing` refuses the head `431` before any handler sees it, pinned by
  `GoogleCloudStorageProtocolServerTests.ServeAsync_HeadPastMaxRequestHead_IsRefused431AndClosedBySurlHttpServing`.
- BL-720 adds the seam in `Surl.HttpServing` that lets a handler word a head-not-read refusal (for S3). Use it
  from `GcsRequestAnswerer` with `GcsError` row `MaxMessageLengthExceeded` (400, `Your request was too big.`).
- Matrix row `GCS:Surl-Limits-1` in `Documentation/Conformance/Extended/GoogleCloudStorage.md` names this task;
  replace it with the test's name.

## Acceptance criteria

- [ ] A named test shows a head past the limit answered `400 MaxMessageLengthExceeded` with the one-line XML body,
      then `Connection: close`.
- [ ] `dotnet build` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
