---
id: BL-891
title: Bind each OCI upload session to the account that opened it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1035]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-891 — Bind each OCI upload session to the account that opened it

## Goal

A `PATCH`, `PUT`, `GET` or `DELETE` on an OCI upload session by a token of another account than the one whose
token opened it is `404 BLOB_UPLOAD_UNKNOWN`, as ADR-0115 code-3 says ("another account's session").

## Context

- ADR-0115 decision 5.1 (code-3) and Amendment 1 point 7. Since BL-438 every write carries an `OciToken`
  (`AccountName`, `null` for anonymous); `OciUploadSession` / `OciUploadSessions` keep sessions per repository only.
- Record the opening token's account on the session and compare it in `OciBlobUploads` before any session answer.

## Acceptance criteria

- [ ] A named test shows a session opened by one account answered `404 BLOB_UPLOAD_UNKNOWN` to another account's
      token with push in the same repository, and served to the account that opened it.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- 2026-11-11: Returned to Backlog behind BL-1035: `dotnet build` fails with "A compatible .NET SDK was not found" (global.json pins 10.0.401; only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed). No code changed.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
