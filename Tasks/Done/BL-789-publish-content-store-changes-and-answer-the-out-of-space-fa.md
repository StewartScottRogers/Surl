---
id: BL-789
title: Publish content store changes and answer the out-of-space fault in Surl.Content
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-789 — Publish content store changes and answer the out-of-space fault in Surl.Content

## Goal

`ContentStore` publishes every change it makes - a file written, replaced, deleted or renamed, a folder created
or removed, object metadata stored - on the event stream, and refuses an upload when an out-of-space fault rule
matches its path, so a test can watch an upload over FTP land before reading it over HTTP and can make uploads
fail on purpose.

## Context

- Decision: BL-822's ADR decisions 1 (`StoreChangedEvent`'s content fields) and 4 (out of space: scope by path or
  share; which result it answers).
- Contracts: BL-823 (`ISurlEventPublisher`, `IFaultPlan`).
- Where: `Foundation/Surl.Content.UnitLibrary` (`ContentStore.cs`, `ContentUploadSession.cs`,
  `ObjectMetadataIndex.cs`). It references `Surl.Protocol.Abstractions` already. Optional constructor parameters
  defaulting to the null objects; BL-803 passes the real ones.
- Which result: the planning default is the existing `ContentUploadResult.TooLarge`, which every file server
  already words (so no server changes here). If the ADR adds a distinct out-of-space result, add it here and file
  one task per file-server family to word it (FTP 452, HTTP 507 and so on, each measured against pinned upstream
  curl 8.21.0), depending on this task.
- An event carries the path (and share or bucket, once ADR-0102's shares exist) and the size, never the bytes.
  A rename is one event, not a delete and a write.
- Stand-in: store = the content store (ADR-0100 decision 1); publishes store changes; seeding and reset are
  BL-800.

## Acceptance criteria

- [x] Named `ContentStoreTests` show one event per change kind above with its path and size, in order, from the
      recording publisher double, for both `InMemoryContentFileSystem` and a `UnitTest...` file system double.
- [x] Named tests show an out-of-space rule for `/uploads` refuses an upload under it with the ADR's result and
      publishes the fault event, while an upload elsewhere succeeds; a rule with `times=1` fires once.
- [x] With no publisher or plan given, every existing `Surl.Content.UnitTests` test passes unchanged.
- [x] `dotnet build Foundation/Surl.Content.UnitLibrary -warnaserror` is clean, the whole solution builds, the
      fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- Decided in ADR-0126 (Claude under Stewart's delegation): ADR-0124's `StoreChange` had no way to say
  renamed, folder created or metadata stored, so the contract grows: `Renamed`, `Created`,
  `MetadataStored` appended, and `StoreChangedEvent.PreviousName` (`init`). Replace and append are
  `Written` with the whole length; a removed folder is `Deleted`. `Name` is the request path with a
  share's name first, never a host path. Setting a last write time is not published.
- Touches widened (rule 3): `Surl.Protocol.Abstractions.UnitLibrary` and `.UnitTests` for the enum and
  property, `Documentation/Planning/Decisions` for ADR-0126. No task in `Doing` named any of them.
- Out of space answers the existing `ContentUploadResult.TooLarge` (the planning default), checked
  before an upload reads a byte and before a session commits; no server changes. The store publishes
  `FaultAppliedEvent` itself, as BL-788 does for mail. A non-`OutOfSpace` rule found at a store write is
  not applied.
- `ExchangeId` is `null` on every content event: the store's API carries no exchange. Filed BL-863.
- Tests: `ContentStoreTests.Events.cs` (both file systems as `DataRow`s), `UnitTestPathPrefixFaultPlan`
  (segment-prefix matching and `times`, since `ScriptedFaultPlan` matches paths exactly and never spends),
  `ObjectMetadataIndexTests.StoreAsync_StoredOrNoSuchFile_...`. `UnitTestInMemoryContentFileSystem` now
  reads back files it was written, so an append after a write works on it.
- Coverage: `Surl.Content.UnitLibrary` and `Surl.Protocol.Abstractions.UnitLibrary` 100% line and branch
  (`Measure-CodeQuality.ps1 -SkipTestRun -Library ...`). Solution build clean, fast tests green.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ContentStore and ObjectMetadataIndex publish every content change, and an out-of-space rule refuses uploads as TooLarge with a FaultAppliedEvent
