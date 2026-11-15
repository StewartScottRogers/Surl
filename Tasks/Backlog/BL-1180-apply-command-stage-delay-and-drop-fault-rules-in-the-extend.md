---
id: BL-1180
title: Apply command-stage delay and drop fault rules in the extended Azure Storage server
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests]
requirement: FR-062
created: 2026-11-14
completed:
---
# BL-1180 — Apply command-stage delay and drop fault rules in the extended Azure Storage server

## Goal

A `delay;stage=command` or `drop;stage=command` fault rule matching an `az`, `abfs` or `abfss` request makes the Azure Storage server hold or cut off that request, instead of ignoring it.

## Context

- Stand-in audit 2026-11-14: `Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary` asks the fault plan for a chosen error only (the `HttpCommandFaults.FindChosenErrorAsync` path; grep `FindChosenErrorAsync` in the library). Switch its command point to `HttpCommandFaults.FindAndApplyAsync` (`Protocols/Http/Surl.HttpMessage.UnitLibrary/HttpCommandFaults.cs`), which applies delay and drop through `CommandStageFaults.FindAndApplyAsync` (ADR-0141) and hands an `error` back, keeping the same point (method, path, account).
- Judged by the Azure Storage REST API reference (ADR-0083, ADR-0109); no upstream curl involved.

## Acceptance criteria

- [ ] Named tests: a `delay` rule at the command stage holds the matching request for `ms` on a fake `TimeProvider`, pausing and restarting the idle clock (`RecordingIdleClock`), then answers it as usual; one `FaultAppliedEvent` (`Delay`, `Command`).
- [ ] Named tests: a `drop` rule at the command stage aborts the connection with no response bytes; one `FaultAppliedEvent` (`Drop`, `Command`).
- [ ] Every existing chosen-error test passes unchanged, and a rule for another path or method changes nothing.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the Azure Storage library keeps 100% line and branch coverage with no method over complexity 10.

## Notes

## Log

- 2026-11-14: Created.
