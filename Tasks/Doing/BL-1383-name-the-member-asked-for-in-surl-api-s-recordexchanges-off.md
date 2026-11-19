---
id: BL-1383
title: Name the member asked for in Surl.Api's RecordExchanges-off message
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-18
completed:
---
# BL-1383 — Name the member asked for in Surl.Api's RecordExchanges-off message

## Goal

With `SurlServerOptions.RecordExchanges` off, every `SurlServer` member that needs the record throws an
`InvalidOperationException` whose message names that member (for example `WaitForEventAsync`), not "to read
Exchanges"; and `SurlServerFiles.PutAsync` and `DeleteAsync` document the `ArgumentException` they let through.

## Context

- Finding 6 (fourth item) of the Surly cookbook bug report of 2026-11-18: `WaitForEventAsync` with recording off
  throws "RecordExchanges is off, so no exchange was recorded; set SurlServerOptions.RecordExchanges to read
  Exchanges.", which misleads a caller that was waiting for an event.
- Cause: the one message in `SurlServer.RecordOrThrow` (`Application/Surl.Api/SurlServer.cs:388-390`), shared by
  `Exchanges`, `DroppedExchangeCount`, `DroppedEventCount`, `FindExchanges`, `FindEvents`, `WaitForExchangeAsync`
  and `WaitForEventAsync` (`SurlServer.cs:60-142`). Pass the member's name in (an explicit `nameof`, or
  `[CallerMemberName]`), e.g. "RecordExchanges is off, so nothing was recorded; set
  SurlServerOptions.RecordExchanges to use WaitForEventAsync."; keep the leading "RecordExchanges is off" that
  `SurlServerTests.Exchanges.cs:46` and `SurlServerTests.Queries.cs:49` check.
- Found while planning BL-1382: `SurlServerFiles.PutAsync` and `DeleteAsync` (`Application/Surl.Api/SurlServerFiles.cs:23-62`)
  throw `ArgumentException` for a path a request may not name (relative, or with `..`), from
  `ContentStore.MapForTheTest` (`Foundation/Surl.Content.UnitLibrary/ContentStore.Control.cs:252-258`), but their
  XML docs list only `ArgumentNullException` and `SurlSeedRefusedException`.
- Stand-in: recording is the instance's subscription to its event stream; nothing about the stream changes.

## Acceptance criteria

- [ ] A named test in `Application/Surl.Api.UnitTests` shows `WaitForEventAsync` with recording off throws
      `InvalidOperationException` whose message starts "RecordExchanges is off" and contains `WaitForEventAsync`
      and not "read Exchanges".
- [ ] A data-driven or per-member test shows each of the other six members' messages names that member.
- [ ] `SurlServerFiles.PutAsync` and `DeleteAsync` each carry `<exception cref="ArgumentException">` saying the path
      is one a request may not name, such as a relative path or one with a `..` segment, and a named test shows
      `PutAsync("reports/b.txt", ...)` throws it.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green, and `Surl.Api` keeps
      its coverage.

## Notes

- No README change is needed for the message; BL-1384 edits `Application/Surl.Api/README.md`.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing.
