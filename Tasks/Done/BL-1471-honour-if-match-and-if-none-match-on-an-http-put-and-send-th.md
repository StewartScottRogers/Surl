---
id: BL-1471
title: Honour If-Match and If-None-Match on an HTTP PUT and send the new ETag (ADR-0184 decision 7)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1449]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Application/Surl.Cli.UnitLibrary/AiHelpProse.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1471 — Honour If-Match and If-None-Match on an HTTP PUT and send the new ETag (ADR-0184 decision 7)

## Goal

A PUT under --allow-uploads honours If-Match and If-None-Match and answers 201/204 with the new file's ETag.

## Context

- ADR-0184 decision 7; BL-1449 shipped the ETag and the GET/HEAD conditions and left this out to stay one run.
- HttpRequestAnswerer.Uploads.cs, HttpRequestAnswerer.Conditions.cs (ETagOf, EntityTagsOf, TagListMatches).

## Acceptance criteria

- [x] Named tests: If-Match mismatch or on a missing file is 412 and the file is unchanged; If-None-Match: * on an existing file is 412; any other If-None-Match is 412 when a listed tag matches.
- [x] If-Unmodified-Since is 412 on a newer file for a PUT; If-Modified-Since is ignored.
- [x] A 201 or 204 carries the new file's ETag.
- [x] The --aihelp http paragraph says so; build clean, fast tests green, 100% coverage.

## Notes

- Decided under Stewart's delegation (ADR-0184 decision 7): a PUT's conditions are judged once the body has arrived and any body-bound login accepted it, just before the commit, so a 412 never leaks a file's tag to an unauthenticated peer and the upload session is disposed uncommitted. Missing file: If-Match is 412, every other condition is met. The ETag on the 201/204 is read from the store after the commit and left out if the status cannot be read.
- The recorded put-created-201 and put-replaced-204 response.bin fixtures now carry the ETag line.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. PUT honours If-Match, If-None-Match, If-Unmodified-Since and sends the new ETag
