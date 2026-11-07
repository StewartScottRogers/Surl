---
id: BL-759
title: Record where Data Lake appends are staged in ADR-0109
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-426]
touches: [Documentation/Planning/Decisions/ADR-0109-how-the-azure-storage-server-answers-az-abfs-and-abfss.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-759 — Record where Data Lake appends are staged in ADR-0109

## Goal

ADR-0109 decision 5.5 says where Data Lake appends are staged and how they are serialised, as BL-426 built it.

## Context

- ADR-0109 decision 5.5 says an append stages its body "under ADR-0100's `.surl/objects/uploads/<upload id>/`".
  BL-426 (decided by Claude under Stewart's delegation, recorded in its Notes) stages it instead in a
  `ContentUploadSession` started from the file's committed bytes: the store's own dot-file beside the
  target (`.surl-upload-<guid>`), which no protocol serves or lists, renamed over the file on flush. The
  content store has no API to write under `.surl/objects/uploads/`, and the session already gives the
  invisibility, the `--max-filesize` bound and the atomic commit the decision asks for.
- Appends and flushes are served one at a time across the server (`AzurePathStagingArea`), because an
  upload session is not safe for concurrent use.
- BL-426 could not edit the ADR: BL-530 held `Documentation/Planning/Decisions` at the time.

## Acceptance criteria

- [x] ADR-0109 decision 5.5 names the upload session beside the target as the staging place and says
      appends and flushes are served one at a time, marked as amended by BL-426's decision.
- [x] `Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary/CLAUDE.md` and the ADR agree.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. ADR-0109 decision 5.5 names the upload session beside the target as the staging place and AzureStagingArea's one-at-a-time serving, agreeing with the library's CLAUDE.md
