---
id: BL-1455
title: Refuse an upload into .surl as not permitted before checking its folder exists
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1455 — Refuse an upload into .surl as not permitted before checking its folder exists

## Goal

A logged-in `curl -T x http://127.0.0.1:<port>/.surl/x` under `--allow-uploads` gets `403 Forbidden`,
as ADR-0031 decision 5 says, instead of `409 Conflict`. Every other protocol's upload into `.surl` gets
its own not-permitted answer.

## Context

- Surly bug report `2026-11-19T2203-Surl-bug-report.md` item 4, Low (Surl `763754785`, unchanged since):
  "A logged-in `PUT` to `/.surl/x` gets 409; 403 or 404 would say the path is reserved."
- ADR-0031 decision 5: "an upload to `/.surl` or anything under it is `NotPermitted`", and "The rule
  applies in memory too, where no `.surl` exists". The HTTP server maps `NotPermitted` to `403` and
  `NoSuchDirectory`/`IsADirectory` to `409`
  (`Protocols/Http/Surl.Protocol.Http.UnitLibrary/HttpRequestAnswerer.Uploads.cs:127-133`).
- Cause: `Foundation/Surl.Content.UnitLibrary/ContentStore.cs:1070-1092` (`UploadOpeningResult`) checks
  `IsDirectlyInsideADirectory` and returns `NoSuchDirectory` before it checks
  `mapping.IsAnsweredAsAbsent` (which `.surl` and hidden paths set). In memory, and for `/.surl/sub/x`
  on disk, the folder does not exist, so 409 comes first. The fix moves the `IsAnsweredAsAbsent` check
  before the directory checks.
- The same ordering serves FTP `STOR`, SFTP, SCP, SMB, TFTP and the object servers, so their tests must
  stay green.

## Acceptance criteria

- [ ] Fast tests in `Foundation/Surl.Content.UnitTests` show `OpenUploadAsync` for `/.surl/x`,
      `/.SURL/x` and `/.surl/a/b` returns `NotPermitted` over an `InMemoryContentFileSystem`, with
      uploads allowed.
- [ ] A hidden dot-file path without `--serve-dot-files` (`/.secret/x`) returns `NotPermitted` too, and
      `/missing/x` still returns `NoSuchDirectory` (named tests).
- [ ] The full fast suite is green (`dotnet test --filter "TestCategory!=Integration"`), so every
      protocol's upload tests still pass.
- [ ] `dotnet build Foundation/Surl.Content.UnitLibrary -warnaserror` is clean and coverage stays at
      100% line and branch.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T2203-Surl-bug-report.md` item 4.
