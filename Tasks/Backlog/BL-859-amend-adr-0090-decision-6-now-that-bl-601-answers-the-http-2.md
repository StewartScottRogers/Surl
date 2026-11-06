---
id: BL-859
title: Amend ADR-0090 decision 6 now that BL-601 answers the HTTP/2 503 refusal
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-601]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-05
completed:
---
# BL-859 — Amend ADR-0090 decision 6 now that BL-601 answers the HTTP/2 503 refusal

## Goal

ADR-0090 decision 6 and its row in `Documentation/Planning/Decisions/README.md` no longer say the `503` refusal is "still written as HTTP/1.1 bytes": an amendment says BL-601 answers it as ADR-0081 decision 5's "Too many connections" row says.

## Context

- BL-601 made `HttpProtocolServer.WriteRefusalAsync` read the first bytes with `Http2ConnectionPreface` and, on the whole preface, refuse through `Surl.Http2`'s `Http2ConnectionRefusal`: empty `SETTINGS`, `HEADERS` `:status 503`, `server: surl`, `content-length: 0` with `END_STREAM` on the client's first stream, `GOAWAY` naming it, `NO_ERROR`; no `HEADERS` (or no bytes at all) before the engine's refusal deadline closes with no bytes.
- BL-601 could not edit `Documentation/Planning/Decisions` because BL-574 held it.

## Acceptance criteria

- [ ] ADR-0090 has an amendment (dated, naming BL-601) saying decision 6's refusal half is now done, and what is sent.
- [ ] The ADR-0090 row in `Documentation/Planning/Decisions/README.md` no longer lists the HTTP/2 `503` refusal as left to a later task.

## Notes

## Log

- 2026-11-05: Created.
