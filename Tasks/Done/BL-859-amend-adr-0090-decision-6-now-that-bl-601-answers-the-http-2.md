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
completed: 2026-11-11
---
# BL-859 — Amend ADR-0090 decision 6 now that BL-601 answers the HTTP/2 503 refusal

## Goal

ADR-0090 decision 6 and its row in `Documentation/Planning/Decisions/README.md` no longer say the `503` refusal is "still written as HTTP/1.1 bytes": an amendment says BL-601 answers it as ADR-0081 decision 5's "Too many connections" row says.

## Context

- BL-601 made `HttpProtocolServer.WriteRefusalAsync` read the first bytes with `Http2ConnectionPreface` and, on the whole preface, refuse through `Surl.Http2`'s `Http2ConnectionRefusal`: empty `SETTINGS`, `HEADERS` `:status 503`, `server: surl`, `content-length: 0` with `END_STREAM` on the client's first stream, `GOAWAY` naming it, `NO_ERROR`; no `HEADERS` (or no bytes at all) before the engine's refusal deadline closes with no bytes.
- BL-601 could not edit `Documentation/Planning/Decisions` because BL-574 held it.

## Acceptance criteria

- [x] ADR-0090 has an amendment (dated, naming BL-601) saying decision 6's refusal half is now done, and what is sent.
- [x] The ADR-0090 row in `Documentation/Planning/Decisions/README.md` no longer lists the HTTP/2 `503` refusal as left to a later task.

## Notes

- Written directly rather than through `align-and-document`: two prose edits, facts checked against `HttpProtocolServer.WriteRefusalAsync` and `Http2ConnectionRefusal` as they stand and against BL-601's notes.
- Choice: decision 6's original text is left as written and Amendment 1 is appended, as other amended ADRs do (ADR-0010 Amendment 1), so the history stays readable.
- Choice: the amendment leaves decision 6's NTLM and Negotiate half to BL-602 and does not say whether BL-602 is done; recording that is outside this task. No `.cs` or project file changed, so no build or test gate applies.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. ADR-0090 Amendment 1 records that BL-601 answers the HTTP/2 503 refusal, and its README row no longer lists it as left to a later task
