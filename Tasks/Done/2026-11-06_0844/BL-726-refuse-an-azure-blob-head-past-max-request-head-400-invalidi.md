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
completed: 2026-11-06
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

- [x] A named Azure test shows a head past the limit answered `400` with `InvalidInput`, `x-ms-request-id`
      matching the body's `RequestId:` line, and `Connection: close`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- Filed by BL-424: the seam is BL-720's, in `Surl.HttpServing`, outside BL-424's `touches`.
- Done (2026-11-06): `AzureBlobRequestAnswerer` and `AzureDataLakeRequestAnswerer` implement BL-720's
  `IHttpHeadNotReadRefuser`; `AzureReply.HeadNotReadRefusal` answers `HeadTooLarge` with `400 InvalidInput`
  (common fields, `x-ms-error-code`, the endpoint's error body, one fresh request ID in both the field and the
  body) and keeps the standard refusal for every other outcome. `Connection: close` and the close come from
  `Http1AnswerWriter.WriteRefusalAsync`.
- Choice: the Data Lake endpoint words it too, in its JSON error body. ADR-0109 decision 7 states the limit for
  the whole server, and its `503 ServerBusy` row already says "in the endpoint's body"; the Blob XML on `abfs`
  would be wrong for that endpoint. No new ADR: decision 7 already decides it.
- No `x-ms-client-request-id` echo: no head was read, so nothing can be echoed.
- Tests: `AzureBlobRequestAnswererTests.RefuseHeadNotRead_HeadPastMaxRequestHead_Is400InvalidInputAndClosed`
  (exact bytes; `ExpectedError` checks the field's ID equals the body's `RequestId:`),
  `AzureDataLakeRequestAnswererTests.RefuseHeadNotRead_HeadPastMaxRequestHead_Is400InvalidInputInTheJsonBodyAndClosed`,
  and a keeps-the-standard-`505` test on each. Azure tests 437; library line and branch coverage 100%.
- `Documentation/Conformance/Extended/AzureStorage.md` row `AzureStorage:Surl-Limits-1` is outside `touches` and
  names no status to change, so it is left as is.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. An az or abfs head past --max-request-head is answered 400 InvalidInput in the endpoint's error body, then closed
