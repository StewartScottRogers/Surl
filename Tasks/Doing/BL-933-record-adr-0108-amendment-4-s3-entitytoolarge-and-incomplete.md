---
id: BL-933
title: Record ADR-0108 Amendment 4: S3 EntityTooLarge and IncompleteBody close without the drain
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-729, BL-1035]
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

- The ADR edit is finished and sits on branch `factory/BL-933-lane-3-20261111-060243` (commit 3b531940). It adds Amendment 4 to ADR-0108 and corrects Amendment 2, point 4. Lane 4 cherry-picked it on 2026-11-11 and checked that `HttpAnswer.DrainsBeforeClose`, `Http1AnswerWriter.DrainBeforeCloseAsync`, `S3Error.ClosesWithoutDrain`, `S3Reply.Error` and both named `S3PutObjectTests` tests exist. Next run: `git cherry-pick --no-commit factory/BL-933-lane-3-20261111-060243`, keep only the ADR file (that commit also carries a stale task-file move to `Done`), tick both boxes and move to Done.
- Lane 3 and lane 6 both failed to integrate for the same reason: the pinned .NET SDK 10.0.401 is not installed on the machine (BL-1035), so `dotnet build` fails in every lane. The ADR change did not cause it. Lane 4 hit the same failure (2026-11-11), so this task now depends on BL-1035 and waits in Backlog instead of being bounced a third time.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 6 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-933-lane-6-20261111-060243; start with git cherry-pick --no-commit factory/BL-933-lane-6-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 3 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-933-lane-3-20261111-060243; start with git cherry-pick --no-commit factory/BL-933-lane-3-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so no lane can build; ADR edit ready on factory/BL-933-lane-3-20261111-060243
- 2026-11-12: Backlog -> Doing.
