---
id: BL-1442
title: Document in the Surl.Api README the ConfigFiles-only start, the new options properties and how long StopAsync waits for open connections
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1439, BL-1440, BL-1441]
touches: [Application/Surl.Api/README.md]
requirement: none
created: 2026-11-20
completed:
---
# BL-1442 — Document in the Surl.Api README the ConfigFiles-only start, the new options properties and how long StopAsync waits for open connections

## Goal

`Application/Surl.Api/README.md` tells a test author that a server can start from `ConfigFiles` alone,
that `AuthenticationMethods` takes a collection initializer, which properties set the TLS range, file
types and head limit, and that `StopAsync` waits up to 7 seconds while a client holds a connection open.

## Context

- Surly bug report `2026-11-19T1942-Surl-bug-report.md` items 3, 4 and 10, `2026-11-19T2203` item 6,
  once BL-1439, BL-1440 and BL-1441 have changed the code.
- Item 9 (`StopAsync` takes about 7 s while a client still holds a connection) is the designed
  shutdown, not a defect: ADR-0099 decision 4 ("after the same shutdown grace period"),
  `ServingEngine.DefaultShutdownGracePeriod` = 5 s (`Foundation/Surl.Core.UnitLibrary/ServingEngine.cs:33`)
  plus `StreamConnection.LingeringCloseTime` = 2 s (`Foundation/Surl.Networking.UnitLibrary/StreamConnection.cs:25`).
  The README's stop bullet (lines 63-65) does not say so; a test that disposes its clients first stops
  in milliseconds.

## Acceptance criteria

- [ ] The README's options section shows a `ConfigFiles`-only start and says when an empty `ListenUrls`
      is an `ArgumentException`, matching BL-1438's amendment.
- [ ] It shows `AuthenticationMethods = { "basic" }` and lists each property BL-1441 added with the option
      it composes.
- [ ] The `StopAsync` bullet states the 5-second grace period for open exchanges plus up to 2 seconds of
      lingering close per connection, and that disposing clients first avoids the wait.
- [ ] Every statement matches the code (`SurlServerOptions.cs`, `SurlServerArguments.cs`,
      `ServingEngine.cs:33`, `StreamConnection.cs:25`).

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T1942-Surl-bug-report.md` items 3, 4, 9 and 10, and `2026-11-19T2203-Surl-bug-report.md` item 6.
