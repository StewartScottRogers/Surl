---
id: BL-601
title: Refuse a connection past a connection limit over HTTP/2 prior knowledge as ADR-0081 says
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-368]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.Http2.UnitLibrary, Protocols/Http/Surl.Http2.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-601 — Refuse a connection past a connection limit over HTTP/2 prior knowledge as ADR-0081 says

## Goal

A cleartext connection past a connection limit that opens with the HTTP/2 preface is refused as ADR-0081 decision 5's "Too many connections" row says: `SETTINGS`, `HEADERS` `:status 503` with `END_STREAM`, and `GOAWAY` naming that stream, `NO_ERROR`.

## Context

- ADR-0081 decision 5 (the row and curl's measured 503, exit 22 with `-f`); ADR-0090 decision 6.
- `HttpProtocolServer.WriteRefusalAsync` writes HTTP/1.1 `503` bytes today, whatever the client speaks. Read the first bytes with `Http2ConnectionPreface` within the refusal's write deadline, then read frames up to the client's first `HEADERS`; no `HEADERS` in time: close with no bytes.
- Filed by BL-368, which built the HTTP/2 connection (`Http2ConnectionServer`, `Http2FrameWriter`).

## Acceptance criteria

- [x] A named test replays `Fixtures/h2-prior-knowledge-get-file` through `WriteRefusalAsync` and gets `SETTINGS`, `HEADERS` stream 1 decoding to `:status 503` with `END_STREAM`, and `GOAWAY` last-stream 1 `NO_ERROR`.
- [x] A named test: a preface with no `HEADERS` within the deadline closes with no bytes.
- [x] An HTTP/1.1 refusal's recorded `refusal-503` bytes are unchanged.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Protocol.Http` keeps 100% line and branch coverage.

## Notes

- Added `Protocols/Http/Surl.Http2.UnitLibrary` and `Surl.Http2.UnitTests` to `touches` (no task in Doing names them): the frame reader and writer are internal there (ADR-0086), so the refusal lives there as public `Http2ConnectionRefusal` rather than a second hand-rolled codec in `Surl.Protocol.Http`.
- `WriteRefusalAsync` now reads the first bytes with `Http2ConnectionPreface` under the engine's refusal-deadline token. The whole preface goes to `Http2ConnectionRefusal`; anything else, and a client that half-closes with no bytes, gets the recorded HTTP/1.1 `refusal-503` bytes unchanged.
- Choice: a client that sends no byte before the deadline now gets no bytes (it used to get the HTTP/1.1 503 unasked). Why: the version is unknown until a byte arrives, ADR-0081 decision 5 closes an HTTP/2 client with no `HEADERS` in time with no bytes, and HTTP/1.x already closes a connection that sent no byte of its head with no bytes. Upstream curl always sends its request at once, so its measured 503 (exit 22 with `-f`) is unchanged.
- Choice: the server's `SETTINGS` is empty (curl accepts an empty one, ADR-0081 decision 5, measured) - the refusal has no `ExchangeContext` limits to advertise and serves no stream. The client's `SETTINGS` is not acknowledged; `GOAWAY` ends the connection. Frames before the first `HEADERS` (SETTINGS, WINDOW_UPDATE, unknown types) are passed over; a close or a malformed frame first writes nothing.
- The `HEADERS` block is `:status 503`, `server: surl`, `content-length: 0`: the HTTP/1.1 fields without `Connection`, which RFC 9113 section 8.2.2 forbids.
- Tests: `ConnectionRefusalTests.WriteRefusalAsync_RecordedPriorKnowledgeGetOfAFile_AnswersSettingsThen503OnStream1ThenGoAwayNoError`, `..._PrefaceWithNoHeadersWithinTheDeadline_ClosesWithNoBytes`, `..._WritesTheRecorded503AndCompletesWritesWithoutAbort` (now fed the recorded `refusal-503` request), plus `Http2ConnectionRefusalTests` in `Surl.Http2.UnitTests`. `HttpProtocolServer` and `Http2ConnectionRefusal` at 100% line and branch.
- ADR-0090 decision 6 still says the refusal waits for BL-601; `Documentation/Planning/Decisions` is held by BL-574, so BL-859 is filed to amend it.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. A connection past a connection limit that opens with the HTTP/2 preface is refused with SETTINGS, HEADERS :status 503 END_STREAM and GOAWAY NO_ERROR naming its stream
