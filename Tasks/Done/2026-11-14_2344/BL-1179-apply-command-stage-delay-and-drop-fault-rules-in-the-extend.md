---
id: BL-1179
title: Apply command-stage delay and drop fault rules in the extended S3 server
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests]
requirement: FR-062
created: 2026-11-14
completed: 2026-11-14
---
# BL-1179 — Apply command-stage delay and drop fault rules in the extended S3 server

## Goal

A `delay;stage=command` or `drop;stage=command` fault rule matching an S3 request makes the S3 server hold or cut off that request, instead of logging "which this server does not apply" and answering as without it.

## Context

- Stand-in audit 2026-11-14: `Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary/S3ChosenErrorFault.cs` asks through `HttpCommandFaults.FindChosenErrorAsync`, which applies only `error`. `HttpCommandFaults.FindAndApplyAsync` (`Protocols/Http/Surl.HttpMessage.UnitLibrary/HttpCommandFaults.cs`) applies delay and drop through `CommandStageFaults.FindAndApplyAsync` (ADR-0141) and hands an `error` back, as the HTTP server uses it.
- ADR-0141 consequences: "Other server families apply the same two kinds by calling the same helper at their command points; nothing in `Surl.Core` or the abstractions changes for them." Same point as today's chosen error (method, path, account), so a rule that matched an `error` matches the same requests as a delay or drop.
- Judged by the S3 API Reference (ADR-0083, ADR-0108); no upstream curl involved.

## Acceptance criteria

- [x] Named tests: a `delay` rule at the command stage holds the matching request for `ms` on a fake `TimeProvider`, pausing and restarting the idle clock (`RecordingIdleClock`), then answers it as usual; one `FaultAppliedEvent` (`Delay`, `Command`).
- [x] Named tests: a `drop` rule at the command stage aborts the connection with no response bytes; one `FaultAppliedEvent` (`Drop`, `Command`).
- [x] Every existing chosen-error test passes unchanged, and a rule for another path or method changes nothing.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the S3 library keeps 100% line and branch coverage with no method over complexity 10.

## Notes

- S3ChosenErrorFault.FindAsync now calls HttpCommandFaults.FindAndApplyAsync and returns the HttpAnswer (chosen error, or a DropsExchange answer as the HTTP server does). Old "delay is served as without it, no event" test replaced by held-then-served and drop tests. Pre-existing format nit in S3DeleteObject.cs (final newline) left alone.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. S3 server applies command-stage delay and drop fault rules
