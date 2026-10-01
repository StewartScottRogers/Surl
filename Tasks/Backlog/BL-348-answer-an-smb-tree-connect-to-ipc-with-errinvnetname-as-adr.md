---
id: BL-348
title: Answer an SMB tree connect to IPC$ with ERRinvnetname as ADR-0073 says
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Protocol.Smb.UnitLibrary, Surl.Protocol.Smb.UnitTests]
requirement: FR-050
created: 2026-10-01
completed:
---
# BL-348 — Answer an SMB tree connect to IPC$ with ERRinvnetname as ADR-0073 says

## Goal

A tree connect to `IPC$` is refused `ERRSRV/ERRinvnetname` whatever the content store holds, as
ADR-0073 states.

## Context

- Found by BL-319: ADR-0073 (line ~201) lists `IPC$` among the shares answered
  `ERRSRV/ERRinvnetname`, but `SmbSession` has no special case for it; it is refused only because a
  directory named `IPC$` does not usually exist in the content store.
- Decide whether the code or the ADR changes; the ADR is the stated intent, so the default is the code.

## Acceptance criteria

- [ ] A test in `Surl.Protocol.Smb.UnitTests` proves a tree connect to `IPC$` is answered
      `ERRSRV/ERRinvnetname` even when the content store holds a directory named `IPC$`.
- [ ] `Surl.Protocol.Smb.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-01: Created.
