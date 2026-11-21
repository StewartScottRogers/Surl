---
id: BL-1467
title: Give the HTTP server's own 4xx and 5xx refusals a short fixed text body (ADR-0186)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1444]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.HttpServing.UnitLibrary, Protocols/Http/Surl.HttpServing.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests, Documentation]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1467 — Give the HTTP server's own 4xx and 5xx refusals a short fixed text body (ADR-0186)

## Goal

Every 4xx and 5xx the HTTP server itself originates carries the fixed body ADR-0186 decides, so `curl --fail-with-body` and `curl -i` show it.

## Context

- ADR-0186 decisions 1 to 4 give the exact bytes, the exceptions (HEAD, 1xx, 204, 304, extended and proxy servers) and the fixtures and tests that change.
- Refusals live in `HttpRequestAnswerer` and `Surl.HttpServing` (`Http1AnswerWriter`, `IHttpHeadNotReadRefuser`).

## Acceptance criteria

- [x] Each 4xx/5xx the server originates answers `<code> <reason>\n` as `text/plain; charset=utf-8` with the matching `Content-Length`.
- [x] HEAD, 304 and every status below 400 are unchanged.
- [x] The fixtures ADR-0186 lists are re-recorded from the pinned upstream curl and the tests pass.

## Notes

- Added to `touches` (no task in Doing names them): the S3, Azure Storage and Google Cloud Storage test projects, whose "keeps the standard 505" tests now see the text body the standard refusal carries, and `Documentation` for ADR-0186 Amendment 1.
- Decided: a status with no reason phrase writes the code alone; `416` (listed by ADR-0186 decision 1) carries the text; the HTTP/2 connection-limit `503` stays empty. Fixtures re-recorded from pinned curl 8.21.0 (list in Fixtures/README.md); exit codes unchanged.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. Every 4xx and 5xx the HTTP server originates carries ADR-0186's text body; fixtures re-recorded from pinned curl.
