---
id: BL-601
title: Refuse a connection past a connection limit over HTTP/2 prior knowledge as ADR-0081 says
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-368]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-601 — Refuse a connection past a connection limit over HTTP/2 prior knowledge as ADR-0081 says

## Goal

A cleartext connection past a connection limit that opens with the HTTP/2 preface is refused as ADR-0081 decision 5's "Too many connections" row says: `SETTINGS`, `HEADERS` `:status 503` with `END_STREAM`, and `GOAWAY` naming that stream, `NO_ERROR`.

## Context

- ADR-0081 decision 5 (the row and curl's measured 503, exit 22 with `-f`); ADR-0090 decision 6.
- `HttpProtocolServer.WriteRefusalAsync` writes HTTP/1.1 `503` bytes today, whatever the client speaks. Read the first bytes with `Http2ConnectionPreface` within the refusal's write deadline, then read frames up to the client's first `HEADERS`; no `HEADERS` in time: close with no bytes.
- Filed by BL-368, which built the HTTP/2 connection (`Http2ConnectionServer`, `Http2FrameWriter`).

## Acceptance criteria

- [ ] A named test replays `Fixtures/h2-prior-knowledge-get-file` through `WriteRefusalAsync` and gets `SETTINGS`, `HEADERS` stream 1 decoding to `:status 503` with `END_STREAM`, and `GOAWAY` last-stream 1 `NO_ERROR`.
- [ ] A named test: a preface with no `HEADERS` within the deadline closes with no bytes.
- [ ] An HTTP/1.1 refusal's recorded `refusal-503` bytes are unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Protocol.Http` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
