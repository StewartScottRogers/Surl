---
id: BL-729
title: Let an S3 EntityTooLarge or IncompleteBody answer close without draining the body
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-419]
touches: [Protocols/Http/Surl.HttpServing.UnitLibrary, Protocols/Http/Surl.HttpServing.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-729 — Let an S3 EntityTooLarge or IncompleteBody answer close without draining the body

## Goal

An S3 `400 EntityTooLarge` or `400 IncompleteBody` answer carries its XML `Error` body and closes the connection without draining the body, as ADR-0108 decision 7 says.

## Context

- `Http1AnswerWriter.WriteRefusalAsync` writes a refusal's head only, so BL-419 sends these as ordinary error answers: `EntityTooLarge` leaves the body unread, which closes after the lingering drain (ADR-0024); `IncompleteBody` after a body read in part leaves the connection open.
- Needs `Surl.HttpServing` to write a refusal's body (or an answer flag meaning close after this answer) without changing the HTTP server's recorded bytes. Related: BL-720.

## Acceptance criteria

- [ ] A named S3 test shows `EntityTooLarge` for an announced body past `--max-filesize` answered with the XML body and `Connection: close`, the body not read.
- [ ] A named S3 test shows `IncompleteBody` answered with the XML body and `Connection: close`.
- [ ] Every existing HTTP test passes unchanged; `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
