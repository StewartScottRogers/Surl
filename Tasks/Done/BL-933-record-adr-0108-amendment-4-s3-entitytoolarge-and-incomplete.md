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
completed: 2026-11-11
---
# BL-933 — Record ADR-0108 Amendment 4: S3 EntityTooLarge and IncompleteBody close without the drain

## Goal

ADR-0108 records, as Amendment 4, the decision BL-729 built: S3's `EntityTooLarge` and `IncompleteBody` are refusals that close without ADR-0024's lingering drain, through `HttpAnswer.DrainsBeforeClose`.

## Context

- BL-729 could not edit ADR-0108 because BL-701 held `Documentation/Planning/Decisions` at the time; its decision and reasons are in BL-729's Notes (in `Tasks/Done` or its archive) - copy them into the amendment, marked "Decided by Claude under Stewart's delegation".
- Amendment 2, point 4 of ADR-0108 still ends "Closing without the drain is BL-729."; it should say BL-729 did it and point to Amendment 4.
- Code: `Protocols/Http/Surl.HttpServing.UnitLibrary/HttpAnswer.cs` (`DrainsBeforeClose`), `Http1AnswerWriter.DrainBeforeCloseAsync`, `Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary/S3Error.cs` (`ClosesWithoutDrain`), `S3Reply.Error`.

## Acceptance criteria

- [x] ADR-0108 has a section `## Amendment 4` naming `HttpAnswer.DrainsBeforeClose` and `S3Error.ClosesWithoutDrain`, saying the close is left to the transport's lingering close (ADR-0021) and why, and marked "Decided by Claude under Stewart's delegation".
- [x] ADR-0108 Amendment 2, point 4 no longer says closing without the drain is still to do.

## Notes

- Done directly rather than through align-and-document: one ADR file, copying BL-729's recorded decision. Amendment 4 has four points (the flag, the S3 errors, the close left to ADR-0021, the tests); Amendment 2 point 4 now says what BL-419 built and that BL-729 replaced it (Amendment 4).
- Only Markdown changed, so the verify skill (build and tests) does not apply; no `.cs` or project file touched. The appended text was normalised to the file's CRLF line endings.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. ADR-0108 Amendment 4 records BL-729's no-drain close for S3 EntityTooLarge and IncompleteBody; Amendment 2 point 4 corrected
