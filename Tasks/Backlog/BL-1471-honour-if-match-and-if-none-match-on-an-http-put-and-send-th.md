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
completed:
---
# BL-1471 — Honour If-Match and If-None-Match on an HTTP PUT and send the new ETag (ADR-0184 decision 7)

## Goal

A PUT under --allow-uploads honours If-Match and If-None-Match and answers 201/204 with the new file's ETag.

## Context

- ADR-0184 decision 7; BL-1449 shipped the ETag and the GET/HEAD conditions and left this out to stay one run.
- HttpRequestAnswerer.Uploads.cs, HttpRequestAnswerer.Conditions.cs (ETagOf, EntityTagsOf, TagListMatches).

## Acceptance criteria

- [ ] Named tests: If-Match mismatch or on a missing file is 412 and the file is unchanged; If-None-Match: * on an existing file is 412; any other If-None-Match is 412 when a listed tag matches.
- [ ] If-Unmodified-Since is 412 on a newer file for a PUT; If-Modified-Since is ignored.
- [ ] A 201 or 204 carries the new file's ETag.
- [ ] The --aihelp http paragraph says so; build clean, fast tests green, 100% coverage.

## Notes

## Log

- 2026-11-20: Created.
