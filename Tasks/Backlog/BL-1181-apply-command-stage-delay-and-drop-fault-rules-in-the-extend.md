---
id: BL-1181
title: Apply command-stage delay and drop fault rules in the extended Google Cloud Storage server
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests]
requirement: FR-062
created: 2026-11-14
completed:
---
# BL-1181 — Apply command-stage delay and drop fault rules in the extended Google Cloud Storage server

## Goal

A `delay;stage=command` or `drop;stage=command` fault rule matching a `gs` request, XML or JSON API, makes the Google Cloud Storage server hold or cut off that request, instead of ignoring it.

## Context

- Stand-in audit 2026-11-14: `Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/GcsRequestFaults.cs` (XML API, called from `GcsRequestAnswerer.cs`) and `GcsJsonFaults.cs` (JSON API, called from `GcsJsonRequestAnswerer.cs`) ask through `HttpCommandFaults.FindChosenErrorAsync`, which applies only `error`. Switch both to `HttpCommandFaults.FindAndApplyAsync` (`Protocols/Http/Surl.HttpMessage.UnitLibrary/HttpCommandFaults.cs`), which applies delay and drop through `CommandStageFaults.FindAndApplyAsync` (ADR-0141) and hands an `error` back, at the same points.
- Judged by the Cloud Storage XML and JSON API pages (ADR-0083, ADR-0110, ADR-0148); no upstream curl involved.

## Acceptance criteria

- [ ] Named tests, for one XML API and one JSON API request each: a `delay` rule at the command stage holds the request for `ms` on a fake `TimeProvider`, pausing and restarting the idle clock (`RecordingIdleClock`), then answers it as usual; one `FaultAppliedEvent` (`Delay`, `Command`).
- [ ] Named tests, XML and JSON: a `drop` rule at the command stage aborts the connection with no response bytes; one `FaultAppliedEvent` (`Drop`, `Command`).
- [ ] Every existing chosen-error test passes unchanged, and a rule for another path or method changes nothing.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage with no method over complexity 10.

## Notes

## Log

- 2026-11-14: Created.
