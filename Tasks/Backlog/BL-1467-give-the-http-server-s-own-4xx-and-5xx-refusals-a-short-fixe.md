---
id: BL-1467
title: Give the HTTP server's own 4xx and 5xx refusals a short fixed text body (ADR-0186)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1444]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.HttpServing.UnitLibrary, Protocols/Http/Surl.HttpServing.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1467 — Give the HTTP server's own 4xx and 5xx refusals a short fixed text body (ADR-0186)

## Goal

Every 4xx and 5xx the HTTP server itself originates carries the fixed body ADR-0186 decides, so `curl --fail-with-body` and `curl -i` show it.

## Context

- ADR-0186 decisions 1 to 4 give the exact bytes, the exceptions (HEAD, 1xx, 204, 304, extended and proxy servers) and the fixtures and tests that change.
- Refusals live in `HttpRequestAnswerer` and `Surl.HttpServing` (`Http1AnswerWriter`, `IHttpHeadNotReadRefuser`).

## Acceptance criteria

- [ ] Each 4xx/5xx the server originates answers `<code> <reason>\n` as `text/plain; charset=utf-8` with the matching `Content-Length`.
- [ ] HEAD, 304 and every status below 400 are unchanged.
- [ ] The fixtures ADR-0186 lists are re-recorded from the pinned upstream curl and the tests pass.

## Notes

## Log

- 2026-11-20: Created.
