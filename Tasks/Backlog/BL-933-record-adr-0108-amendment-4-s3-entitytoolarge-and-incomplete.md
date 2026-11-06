---
id: BL-933
title: Record ADR-0108 Amendment 4: S3 EntityTooLarge and IncompleteBody close without the drain
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-729]
touches: [Documentation/Planning/Decisions/ADR-0108-how-the-s3-server-answers-s3.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-933 — Record ADR-0108 Amendment 4: S3 EntityTooLarge and IncompleteBody close without the drain

## Goal

ADR-0108 records, as Amendment 4, the decision BL-729 built: S3's `EntityTooLarge` and `IncompleteBody` are refusals that close without ADR-0024's lingering drain, through `HttpAnswer.DrainsBeforeClose`.

## Context

- BL-729 could not edit ADR-0108 because BL-701 held `Documentation/Planning/Decisions` at the time; its decision and reasons are in BL-729's Notes (in `Tasks/Done` or its archive) - copy them into the amendment, marked "Decided by Claude under Stewart's delegation".
- Amendment 2, point 4 of ADR-0108 still ends "Closing without the drain is BL-729."; it should say BL-729 did it and point to Amendment 4.
- Code: `Protocols/Http/Surl.HttpServing.UnitLibrary/HttpAnswer.cs` (`DrainsBeforeClose`), `Http1AnswerWriter.DrainBeforeCloseAsync`, `Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary/S3Error.cs` (`ClosesWithoutDrain`), `S3Reply.Error`.

## Acceptance criteria

- [ ] ADR-0108 has a section `## Amendment 4` naming `HttpAnswer.DrainsBeforeClose` and `S3Error.ClosesWithoutDrain`, saying the close is left to the transport's lingering close (ADR-0021) and why, and marked "Decided by Claude under Stewart's delegation".
- [ ] ADR-0108 Amendment 2, point 4 no longer says closing without the drain is still to do.

## Notes

## Log

- 2026-11-06: Created.
