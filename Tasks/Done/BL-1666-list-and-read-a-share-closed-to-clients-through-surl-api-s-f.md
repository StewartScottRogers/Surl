---
id: BL-1666
title: List and read a share closed to clients through Surl.Api's Files as the test's own view
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-1666 — List and read a share closed to clients through Surl.Api's Files as the test's own view

## Goal

`SurlServer.Files.ListAsync` and `SurlServer.Files.ReadAsync` read the content store as the test, the server's
owner, so a share whose `ShareAccess` rules close it to clients is listed and read as it is without `ShareAccess`,
instead of throwing `No folder at /team`.

## Context

- Source: the Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-24T1330-Surl-bug-report.md`,
  finding 4 (Low), against Surl v0.1.5 `d64faa4bb` on Windows 11 (seen by the recipe writers, not re-run).
  Repro, copied from the report:
  - Surl.Api: an FTP server with a `/team` folder and a `ShareAccess` rule that closes it (recipe
    `ftp-give-a-group-write-access-in-csharp`; the writer's probe was not kept).
  - `await server.Files.ListAsync("/team")` throws `No folder at /team`.
  - Expected: the folder's listing, as without `ShareAccess`. The test API is the server owner's view and should
    not be bound by client share rules.
- Cause: `Application/Surl.Composition.UnitLibrary/ComposedStores.cs`, `ListFolder` and `ReadFileAsync`, map the
  path with `ContentStore.MapRequestPath`, which answers as an anonymous caller (so a share with grants is absent,
  ADR-0102 decision 2.3). The test's own caller already exists: `ContentStore.Control.cs` has the private
  `TheTest` caller and `MapForTheTest`, which `AccessOf` (`ContentStore.cs`) grants `ReadWrite` on every share,
  and which `PutAsync`/`DeleteAsync`'s seeding paths (`SeedFile...`, `DeleteFileForTheTest`) already use.
- Fix: expose a test-view mapping from `Foundation/Surl.Content.UnitLibrary` (e.g. a public
  `MapRequestPathForTheTest`) and use it in `ComposedStores.ListFolder` and `ReadFileAsync`. The root listing then
  shows a closed share as a folder too (`ListSharesInPlaceOfTheirNamesakes` asks `AccessOf` with the mapping's
  caller). `/.surl` paths stay hidden, the path-refusal rules stay, and nothing is published (ADR-0164 decisions 1
  to 4). `Surl.Mcp`'s `list_files` and `read_file` tools go through the same `Files` methods and are fixed with it.
- Stand-in: reads the shared content store; publishes nothing and asks no fault rule, as ADR-0164 says. This is
  exactly how a test checks what a client wrote into a share it cannot see.
- Not curl-facing: no bytes on the wire change.

## Acceptance criteria

- [x] A named test in `Foundation/Surl.Content.UnitTests` shows the test-view mapping reaches a share whose grants
      give an anonymous caller nothing, and a share limited by `Schemes`, while `MapRequestPath` still answers both
      as absent.
- [x] Named tests in `Application/Surl.Composition.UnitTests` (`ComposedStoresTests`) show `ListFolder` lists, and
      `ReadFileAsync` reads, inside a share closed by its grants, and that `ListFolder("/")` lists the share.
- [x] A named regression test in `Application/Surl.Api.UnitTests` starts a server with a `/team` share closed by
      `ShareAccess`, seeds a file with `Files.PutAsync`, and gets it back from `Files.ListAsync("/team")` and
      `Files.ReadAsync`.
- [x] `SurlServerFiles.ListAsync`'s and `ReadAsync`'s XML docs say they read as the server's owner, whatever share
      access says.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Content.UnitLibrary` and `Surl.Composition.UnitLibrary` keep 100% line and branch coverage on every
      member this task changes; no test needs `TestCategory=Integration`.

## Notes

- Decision: `MapRequestPathForTheTest` returns the mapping (refusal as a result) so ComposedStores keeps its Refused outcome.
- `SurlServer.cs` (Surl.Api) now passes `host.DataDirectoryFileSystem` as the composition's `ShareFileSystem` (null keeps the disk), so a host with an in-memory file system gets in-memory shares and the regression test needs no disk.
- `touches` names `Application/Surl.Api` only for the doc comments in `SurlServerFiles.cs`, and
  `Application/Surl.Api.UnitTests` only for the one regression test; no behaviour changes in the host.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. Surl.Api Files.ListAsync/ReadAsync read a share closed by ShareAccess as the server owner
