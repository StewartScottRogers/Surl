---
id: BL-660
title: Pass the SMB login and scheme to the content store's share access check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-656]
touches: [Protocols/FileTransfer/Surl.Protocol.Smb.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-660 — Pass the SMB login and scheme to the content store's share access check

## Goal

The SMB server hands the content store the session's account (or guest) and scheme (`smb` or `smbs`) on tree
connect, create, read and write, and answers a share-access refusal with the DOS-class status BL-642's ADR
measured; configured shares appear as SMB shares exactly as top-level folders do today.

## Context

- Decision: BL-642's ADR decision 2 (the status for a denied tree connect, read and write, e.g.
  `ERRDOS/ERRnoaccess`, measured against the static-curl 8.21.0 Windows build of ADR-0030 with curl's exit code).
  ADR-0073 (how the SMB server answers: each top-level directory a share), BL-656's caller API.
- Where: `Protocols/FileTransfer/Surl.Protocol.Smb.UnitLibrary`.
- Oracle: pinned upstream curl only. `UpstreamCurlTransfersFilesWithSurlOverSmbTests` must pass unchanged.

## Acceptance criteria

- [x] Named tests replay sessions where: an `r` account reads a share and a write is refused with the ADR's
      status; an `rw` account writes; an account with no access is refused at tree connect or create as the ADR
      says; a guest under `--allow-anonymous` gets the ADR's default; with no access configured every existing
      test passes unchanged.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

- Plan (ADR-0102 decisions 2.2 and 2.3 already decide every byte; no new ADR, nothing to measure): the
  accepted session setup sets the session's `ContentCaller` - `ForAccount(verdict.AccountName, scheme)`,
  or `Anonymous(scheme)` for a login `--allow-anonymous` accepted unchecked (no account name) - with the
  listen URL's scheme (`smb` or `smbs`). Tree connect and NT create map through
  `ContentStore.MapRequestPathForCaller`; reads and writes use the mapping the create made, which carries
  the caller. A share the caller may not read is answered as absent, so tree connect gives
  `ERRSRV/ERRinvnetname` exactly as for a missing share, and an upload open in a share it may only read
  gets `ContentUploadOpeningResult.NotPermitted`, already `ERRDOS/ERRnoaccess`. No new SMB reply.
- Default chosen: an `Accepted` verdict naming no account (null or empty), which no policy returns today,
  is answered as an anonymous caller rather than throwing, so it can never open a closed share.
- Tests: `SmbProtocolServerShareAccessTests` (10 cases). `SmbTestExchange.Context`/`ServeAsync` gained an
  optional `scheme` (default `smb`) for the `protocols=smbs` cases.
- Verified: `dotnet build -warnaserror` clean; fast tests green (Smb 206); SMB library 100% line and
  branch (cobertura from the MSTest collector); `UpstreamCurlTransfersFilesWithSurlOverSmbTests` 24/24
  against the pinned build, unchanged.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. SMB hands the content store the session's account or anonymous caller and smb/smbs scheme; closed shares answer ERRinvnetname, denied writes ERRnoaccess
