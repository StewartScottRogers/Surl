---
id: BL-726
title: Refuse an Azure Blob head past --max-request-head 400 InvalidInput
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-424, BL-720]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-726 — Refuse an Azure Blob head past --max-request-head 400 InvalidInput

## Goal

A request head past `--max-request-head` on the `az` server is answered `400 Bad Request` with the Blob XML
`Error` body `<Code>InvalidInput</Code>`, Message `One of the request inputs is not valid.`, and
`x-ms-error-code: InvalidInput`, then closed, as ADR-0109 decision 7 says.

## Context

- ADR-0109 decision 7's first row; matrix row `AzureStorage:Surl-Limits-1` in
  `Documentation/Conformance/Extended/AzureStorage.md`.
- Today `Surl.HttpServing` answers every head it cannot read itself (`431` for a head too large) before the
  Azure server's `IHttpRequestHandler` sees it. BL-720 adds the seam letting a handler word that refusal, for S3;
  this task uses it in `Surl.Extended.Protocol.AzureStorage`.

## Acceptance criteria

- [ ] A named Azure test shows a head past the limit answered `400` with `InvalidInput`, `x-ms-request-id`
      matching the body's `RequestId:` line, and `Connection: close`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- Filed by BL-424: the seam is BL-720's, in `Surl.HttpServing`, outside BL-424's `touches`.

## Log

- 2026-11-05: Created.
