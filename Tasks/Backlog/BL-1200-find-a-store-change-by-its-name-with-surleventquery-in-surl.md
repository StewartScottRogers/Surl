---
id: BL-1200
title: Find a store change by its name with SurlEventQuery in Surl.Api
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1194]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: FR-060
created: 2026-11-15
completed:
---
# BL-1200 — Find a store change by its name with SurlEventQuery in Surl.Api

## Goal

`SurlEventQuery` gains the name filter BL-1194's ADR decides, so `FindEvents` and `WaitForEventAsync` find the `StoreChangedEvent` for one path, mailbox, topic, queue, key or account.

## Context

- Decision: the ADR filed by BL-1194 (the property's name, and whether it matches exactly or as a segment prefix).
- Where: `Application/Surl.Api/SurlEventQuery.cs` (it has `Store` today, line 43) and `SurlEventFacts.Matches`, which already checks `Store` in `IsOfKindAndStore`. `StoreChangedEvent.Name` is the value to match (ADR-0124 decision 1).
- Stand-in fit: `await server.WaitForEventAsync(new SurlEventQuery { Store = StoreKind.Content, Name = "/uploads/report.csv" }, timeout)` waits for one upload instead of filtering every content event in test code.

## Acceptance criteria

- [ ] `SurlEventQuery` has the ADR's name property with an XML doc comment, and `SurlEventFacts` applies it to `StoreChangedEvent` only. An event of another kind never matches a query that sets it.
- [ ] Tests in `Application/Surl.Api.UnitTests` show `FindEvents` with the name returns only that path's write after two files are seeded, and `WaitForEventAsync` with it completes on that write and not on another.
- [ ] If the ADR chooses segment-prefix matching, a test shows `/uploads` matches `/uploads/a.txt` but not `/uploads2/a.txt`.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-15: Created.
