---
id: BL-922
title: Record BL-715's block staging choices in ADR-0109
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-715]
touches: [Documentation/Planning/Decisions/ADR-0109-how-the-azure-storage-server-answers-az-abfs-and-abfss.md]
requirement: none
created: 2026-11-06
completed: 2026-11-11
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
- Done (2026-11-11): decision 7 gains "Where blocks are staged" (the never-committed upload session beside the blob, as decision 5.5 stages appends), and a new "Amendment, 2026-11-11 (BL-715)" records the other choices with their reasons: folders at the first block, the `--max-filesize` bound and discard, a repeated block ID, the in-memory committed list, the shared 1000-staging cap, `blocklisttype`, and the Put Block List answer and XML reading. The BL-715 Context line already no longer said `.surl/objects/uploads/` (BL-759 rewrote decision 5.5); the only remaining mention says why it is not used.
- `dotnet build` could not run: the pinned SDK 10.0.401 is not installed (BL-1035), which fails every build at HEAD whatever the task. The diff is one Markdown file inside the Documentation shared project, so it cannot change the build or test result; finished rather than parked behind BL-1035.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. ADR-0109 records where Put Block stages blocks and BL-715's other block choices with their reasons
