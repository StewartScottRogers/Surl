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
completed: 2026-10-01
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

- [x] A test in `Surl.Protocol.Smb.UnitTests` proves a tree connect to `IPC$` is answered
      `ERRSRV/ERRinvnetname` even when the content store holds a directory named `IPC$`.
- [x] `Surl.Protocol.Smb.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- The code changed, not the ADR: ADR-0073 decision 2 already states that `IPC$` is answered
  `ERRSRV/ERRinvnetname`, and the ADR is the stated intent. `SmbSession.IsShare` now refuses
  `IPC$` before it consults the content store.
- Choice: the comparison ignores case (`OrdinalIgnoreCase`), because SMB share names are not
  case-sensitive, so `ipc$` names the same reserved share. This only makes the existing ADR
  rule hold in every case, so it needed no new ADR.
- Test: `SmbProtocolServerTests.TreeConnect_ToIpcShare_IsInvalidNetworkNameEvenWhenTheStoreHoldsThatDirectory`
  (`IPC$` and `ipc$`) adds that directory to the store and shows the connect is refused. A
  control directory `dollar$` connects in the same session, so the refusal comes from the
  name and not from the `$`. Coverage measured with the MSTest collector: line rate 1, branch rate 1.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. A tree connect to IPC$ is answered ERRSRV/ERRinvnetname whatever the content store holds
