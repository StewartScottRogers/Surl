---
id: BL-922
title: Record BL-715's block staging choices in ADR-0109
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-715, BL-1035]
touches: [Documentation/Planning/Decisions/ADR-0109-how-the-azure-storage-server-answers-az-abfs-and-abfss.md]
requirement: none
created: 2026-11-06
completed: 2026-11-12
---
# BL-922 — Record BL-715's block staging choices in ADR-0109

## Goal

ADR-0109 says where and how Put Block stages blocks, as BL-715 built it, marked "Decided by Claude under
Stewart's delegation".

## Context

- ADR-0109 decision 1 and its BL-715 Context line say blocks are staged under ADR-0100's
  `.surl/objects/uploads/`. `ContentStore` refuses every write under `/.surl`, so BL-715 staged them as
  `AzurePathUpdate` stages Data Lake appends: in a `ContentUploadSession` opened at the blob and never
  committed - a hidden dot-file beside the blob (the same question for appends is BL-759).
- The other choices BL-715 made, all in
  `Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary/CLAUDE.md` and the BL-715 task's Notes:
  the blob's folders are created at its first Put Block; the staged bytes of one blob together are held to
  `--max-filesize` (past it every uncommitted block is discarded, `413`); a block put again replaces the old;
  the committed block list is remembered in memory against the file's length and last-write time and
  forgotten when the blob is written another way; the 1000-staging cap is shared by Data Lake appends and
  blobs' blocks, and at the cap the least recently touched staging holding only a committed list is
  forgotten; `blocklisttype` is case-insensitive; Get Block List of a blob written whole lists no committed
  blocks.

## Acceptance criteria

- [x] ADR-0109 decision 1 or 7 states where blocks are staged, and the BL-715 Context line no longer says
      `.surl/objects/uploads/`.
- [x] ADR-0109 records each of the other choices listed in Context, with its reason.

## Notes

- Filed by BL-715.
- 2026-11-11 (lane 4): the ADR-0109 edit is finished and sits on branch factory/BL-922-lane-6-20261111-060243 (commit 86dba2a3; take only its ADR file, the task file there is in Done). `dotnet build` fails at HEAD for every task because global.json pins SDK 10.0.401, which is not installed (BL-1035), so the shift cannot integrate even this Markdown-only change. Returned to Backlog behind BL-1035, as BL-920 and BL-927 were.

- 2026-11-12 (lane 1): the old branch's ADR file predated the BL-758 and BL-717 amendments, so its patch was applied with a three-way merge and both sides kept. Remaining `.surl/objects/uploads/` mentions only say why it is not used. Markdown-only change; no code touched.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 6 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-922-lane-6-20261111-060243; start with git cherry-pick --no-commit factory/BL-922-lane-6-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build fails; ADR edit is ready on factory/BL-922-lane-6-20261111-060243
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. ADR-0109 records where Put Block stages blocks and BL-715's other block choices
